using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;

namespace OC2DIYLevelDebugLog
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class DebugLogPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "oc2.oc2diylevel.debuglog";
        public const string PluginName = "OC2DIYLevel DebugLog";
        public const string PluginVersion = "2.0.1";

        /// <summary>Unity 原生、无托管堆栈、且不影响游玩的错误前缀。真机上
        /// Screen position out of view frustum 会逐帧刷（非 16:9 letterbox 相机 +
        /// 第三方/上游调用方），_MainTex 也偶发一次；它们不是关卡/运行时故障，
        /// 不能当作「玩家需要上报的严重错误」逐条写进 player.log。</summary>
        private static readonly string[] BenignNativeErrorPrefixes = new string[]
        {
            "Screen position out of view frustum",
            "Material doesn't have a texture property",
        };

        private sealed class SpamState
        {
            public int Count;
            public int Reported;
            public DateTime LastSummaryUtc;
        }

        private readonly object _lock = new object();
        private readonly Queue<string> _queue = new Queue<string>();
        private readonly Queue<string> _bepInExQueue = new Queue<string>();
        private readonly Queue<string> _playerQueue = new Queue<string>();
        private Thread _writer;
        private AutoResetEvent _wake;
        private volatile bool _stopping;
        private string _logDir;
        private string _infoPath;
        private string _debugPath;
        private string _bepInExPath;
        private string _playerPath;
        private bool _enabled = true;
        private bool _captureUnity = true;
        private bool _captureBepInEx = true;
        private bool _capturePlayer = true;
        private bool _captureFps;
        private bool _captureStack = true;
        private bool _captureCheckpoints = true;
        private string _level = "info";
        private int _maxQueue = 512;
        private int _maxSessions = 21;
        private int _dedupeSeconds = 5;
        private bool _suppressNativeSpam = true;
        private int _maxPlayerPromptsPerPattern = 20;
        private int _infoSpamSummarySeconds = 30;
        private readonly Dictionary<string, SpamState> _infoSpamStates = new Dictionary<string, SpamState>();
        private readonly Dictionary<string, SpamState> _playerSpamStates = new Dictionary<string, SpamState>();
        private string _lastMessage;
        private DateTime _lastMessageAt;
        private string _lastPlayerMessage;
        private DateTime _lastPlayerMessageAt;
        private float _fpsElapsed;
        private int _fpsFrames;
        private float _fpsMin;
        private float _fpsMax;
        private BepInExListener _bepInExListener;

        private void Awake()
        {
            var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrEmpty(dir))
                dir = Paths.PluginPath;
            LoadConfig(Path.Combine(dir, "log_config.txt"), dir);
            if (!_enabled)
                return;

            try
            {
                _logDir = CreateSessionDirectory(dir);
                _infoPath = Path.Combine(_logDir, "info.log");
                _debugPath = Path.Combine(_logDir, "debug.log");
                _bepInExPath = Path.Combine(_logDir, "BepInExDebugLog.log");
                _playerPath = Path.Combine(_logDir, "player.log");
                WriteLine(_infoPath, Header("info"));
                WriteLine(_playerPath, Header("player"));
                WriteLine(_bepInExPath, Header("BepInEx"));
            }
            catch (Exception ex)
            {
                _enabled = false;
                Debug.LogError("[OC2DIYLevel DebugLog] 创建日志目录失败: " + ex);
                return;
            }

            _wake = new AutoResetEvent(false);
            _writer = new Thread(WriterLoop);
            _writer.IsBackground = true;
            _writer.Name = "OC2DIYLevel.DebugLog.Writer";
            _writer.Start();
            if (_captureUnity)
                Application.logMessageReceivedThreaded += OnUnityLog;
            RegisterBepInExListener();
            if (_captureFps)
            {
                _fpsMin = float.MaxValue;
                _fpsMax = 0f;
            }
            EnqueueInfo("日志系统已启动，目录=" + _logDir + "，最高等级=" + _level);
        }

        private void Update()
        {
            if (!_enabled || !_captureFps)
                return;
            var delta = Time.unscaledDeltaTime;
            if (delta <= 0f)
                return;
            _fpsElapsed += delta;
            _fpsFrames++;
            if (delta < _fpsMin) _fpsMin = delta;
            if (delta > _fpsMax) _fpsMax = delta;
            if (_fpsElapsed < 1f) return;
            var average = _fpsFrames / _fpsElapsed;
            EnqueueDebug("[FPS] avg=" + average.ToString("F1")
                + " min=" + (_fpsMax > 0f ? (1f / _fpsMax).ToString("F1") : "0")
                + " max=" + (_fpsMin > 0f && _fpsMin < float.MaxValue ? (1f / _fpsMin).ToString("F1") : "0"));
            _fpsElapsed = 0f;
            _fpsFrames = 0;
            _fpsMin = float.MaxValue;
            _fpsMax = 0f;
        }

        private void OnDestroy()
        {
            if (_captureUnity)
                Application.logMessageReceivedThreaded -= OnUnityLog;
            if (_bepInExListener != null)
            {
                try { BepInEx.Logging.Logger.Listeners.Remove(_bepInExListener); } catch { }
            }
            _stopping = true;
            if (_wake != null) _wake.Set();
            if (_writer != null && !_writer.Join(1500)) _writer = null;
            if (_wake != null) _wake.Close();
        }

        private void OnUnityLog(string condition, string stackTrace, LogType type)
        {
            if (!_enabled || !ShouldCapture(type)) return;
            var message = condition ?? "(empty)";
            var severe = type == LogType.Error || type == LogType.Exception || type == LogType.Assert;
            var benign = severe && _suppressNativeSpam && IsBenignNativeError(message);

            var emitInfo = true;
            var emitPlayer = false;
            string infoSummary = null;
            string playerSummary = null;

            lock (_lock)
            {
                if (_lastMessage == message && (DateTime.UtcNow - _lastMessageAt).TotalSeconds < _dedupeSeconds)
                    return;
                _lastMessage = message;
                _lastMessageAt = DateTime.UtcNow;

                if (benign)
                    emitInfo = TrackSpam(_infoSpamStates, "benign:" + BenignPrefixFor(message), out infoSummary);
                if (severe && !benign)
                    emitPlayer = TrackPlayerPrompt(NormalizePattern(message), out playerSummary);
            }

            if (emitInfo)
            {
                var line = Stamp(LevelFor(type), "Unity", message);
                if (_captureStack && !string.IsNullOrEmpty(stackTrace)) line += Environment.NewLine + stackTrace;
                if (benign)
                    line += Environment.NewLine + "（同类原生错误已限流：后续只做计数汇总，不再逐条记录）";
                EnqueueInfo(line);
            }
            else if (infoSummary != null)
            {
                EnqueueInfo(Stamp("WARN", "Unity", infoSummary));
            }

            if (emitPlayer)
                EnqueuePlayer("游戏运行时出现严重错误，请将本次日志目录发送给开发者。\n" + message);
            else if (playerSummary != null)
                EnqueuePlayer(playerSummary);
        }

        /// <summary>原生良性错误判定（仅前缀匹配，见 BenignNativeErrorPrefixes）。</summary>
        private static bool IsBenignNativeError(string message)
        {
            for (int i = 0; i < BenignNativeErrorPrefixes.Length; i++)
            {
                if (message.StartsWith(BenignNativeErrorPrefixes[i], StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static string BenignPrefixFor(string message)
        {
            for (int i = 0; i < BenignNativeErrorPrefixes.Length; i++)
            {
                if (message.StartsWith(BenignNativeErrorPrefixes[i], StringComparison.Ordinal))
                    return BenignNativeErrorPrefixes[i];
            }
            return message;
        }

        /// <summary>模式级计数：首条放行（含完整样本/堆栈），之后按 infoSpamSummarySeconds
        /// 汇总一次，防原生错误逐帧刷屏把 info.log 撑爆。</summary>
        private bool TrackSpam(Dictionary<string, SpamState> states, string key, out string summary)
        {
            summary = null;
            SpamState state;
            if (!states.TryGetValue(key, out state))
            {
                state = new SpamState();
                states[key] = state;
            }
            state.Count++;
            if (state.Count == 1)
            {
                state.Reported = 1;
                state.LastSummaryUtc = DateTime.UtcNow;
                return true;
            }
            if ((DateTime.UtcNow - state.LastSummaryUtc).TotalSeconds < _infoSpamSummarySeconds)
                return false;
            state.LastSummaryUtc = DateTime.UtcNow;
            summary = "已抑制 " + (state.Count - state.Reported) + " 条重复错误（累计 " + state.Count
                + " 条，模式: " + key + "）";
            state.Reported = state.Count;
            return false;
        }

        /// <summary>玩家提示限流：同一模式（数字归一）最多逐条提示
        /// maxPlayerPromptsPerPattern 次，之后每 infoSpamSummarySeconds 汇总一次。</summary>
        private bool TrackPlayerPrompt(string key, out string summary)
        {
            summary = null;
            SpamState state;
            if (!_playerSpamStates.TryGetValue(key, out state))
            {
                state = new SpamState();
                _playerSpamStates[key] = state;
            }
            state.Count++;
            if (state.Count == 1)
                state.LastSummaryUtc = DateTime.UtcNow;
            if (state.Reported < _maxPlayerPromptsPerPattern)
            {
                state.Reported++;
                return true;
            }
            if ((DateTime.UtcNow - state.LastSummaryUtc).TotalSeconds < _infoSpamSummarySeconds)
                return false;
            state.LastSummaryUtc = DateTime.UtcNow;
            summary = "同类错误过多，已暂停逐条提示（累计 " + state.Count + " 条，模式: " + key + "）";
            return false;
        }

        /// <summary>数字/坐标归一，作为玩家提示限流的分组键（避免逐帧变化的数值绕过）。</summary>
        private static string NormalizePattern(string message)
        {
            var sb = new StringBuilder(message.Length);
            var inNumber = false;
            for (int i = 0; i < message.Length; i++)
            {
                var c = message[i];
                if ((c >= '0' && c <= '9') || c == '.' || c == '-' || c == '+')
                {
                    if (!inNumber)
                    {
                        sb.Append('#');
                        inNumber = true;
                    }
                }
                else
                {
                    sb.Append(c);
                    inNumber = false;
                }
            }
            var result = sb.ToString();
            return result.Length > 120 ? result.Substring(0, 120) : result;
        }

        private void RegisterBepInExListener()
        {
            if (!_captureBepInEx) return;
            try
            {
                _bepInExListener = new BepInExListener(this);
                BepInEx.Logging.Logger.Listeners.Add(_bepInExListener);
                EnqueueDebug("BepInEx 日志捕捉已启用");
            }
            catch (Exception ex)
            {
                EnqueueInfo(Stamp("WARN", "DebugLog", "BepInEx 日志捕捉注册失败: " + ex));
                EnqueuePlayer("无法捕捉 BepInEx 诊断日志，游戏功能不受影响。\n" + ex.Message);
            }
        }

        internal void OnBepInExLog(LogEventArgs args)
        {
            if (!_enabled || args == null) return;
            var source = args.Source == null ? "BepInEx" : args.Source.SourceName;
            var data = args.Data == null ? "" : args.Data.ToString();
            var line = Stamp(args.Level.ToString(), source, data);
            lock (_lock)
            {
                if (_bepInExQueue.Count >= _maxQueue) _bepInExQueue.Dequeue();
                _bepInExQueue.Enqueue(line);
            }
            if (args.Level == LogLevel.Error || args.Level == LogLevel.Fatal)
                EnqueuePlayer("BepInEx 报告了严重错误，详细内容请查看 BepInExDebugLog.log。\n" + data);
            else if (data.IndexOf("[PLAYER]", StringComparison.OrdinalIgnoreCase) >= 0)
                EnqueuePlayer(data.Replace("[PLAYER]", "").Trim());
            if (_wake != null) _wake.Set();
        }

        private bool ShouldCapture(LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) return true;
            if (type == LogType.Warning) return _level == "debug" || _level == "info" || _level == "warning";
            return _level == "debug" || _level == "info";
        }

        private string LevelFor(LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) return "ERROR";
            if (type == LogType.Warning) return "WARN";
            return "INFO";
        }

        internal void EnqueueInfo(string message) { Enqueue(_queue, Stamp("INFO", "DebugLog", message)); }
        internal void EnqueueDebug(string message)
        {
            if (_level == "debug" && _captureCheckpoints) Enqueue(_queue, Stamp("DEBUG", "DebugLog", message));
        }
        internal void EnqueuePlayer(string message)
        {
            if (!_capturePlayer) return;
            lock (_lock)
            {
                if (_lastPlayerMessage == message && (DateTime.UtcNow - _lastPlayerMessageAt).TotalSeconds < _dedupeSeconds) return;
                _lastPlayerMessage = message;
                _lastPlayerMessageAt = DateTime.UtcNow;
                if (_playerQueue.Count >= _maxQueue) _playerQueue.Dequeue();
                _playerQueue.Enqueue(Stamp("严重", "玩家提示", message));
            }
            if (_wake != null) _wake.Set();
        }

        private void Enqueue(Queue<string> queue, string line)
        {
            lock (_lock)
            {
                if (queue.Count >= _maxQueue) queue.Dequeue();
                queue.Enqueue(line);
            }
            if (_wake != null) _wake.Set();
        }

        private void WriterLoop()
        {
            while (!_stopping)
            {
                if (_wake != null) _wake.WaitOne(1000);
                Flush();
            }
            Flush();
        }

        private void Flush()
        {
            List<string> info = null;
            List<string> bep = null;
            List<string> player = null;
            lock (_lock)
            {
                if (_queue.Count > 0) { info = new List<string>(_queue); _queue.Clear(); }
                if (_bepInExQueue.Count > 0) { bep = new List<string>(_bepInExQueue); _bepInExQueue.Clear(); }
                if (_playerQueue.Count > 0) { player = new List<string>(_playerQueue); _playerQueue.Clear(); }
            }
            try
            {
                if (info != null)
                {
                    Append(_infoPath, info);
                    if (_level == "debug") Append(_debugPath, info);
                }
                if (bep != null) Append(_bepInExPath, bep);
                if (player != null) Append(_playerPath, player);
            }
            catch { _enabled = false; }
        }

        private void LoadConfig(string path, string dir)
        {
            if (!File.Exists(path))
            {
                try { File.WriteAllText(path, DefaultConfig()); } catch { }
                return;
            }
            try
            {
                var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var raw in File.ReadAllLines(path))
                {
                    var line = raw == null ? "" : raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    var at = line.IndexOf('=');
                    if (at <= 0) continue;
                    values[line.Substring(0, at).Trim()] = line.Substring(at + 1).Trim();
                }
                _enabled = ReadBool(values, "enabled", true);
                _captureUnity = ReadBool(values, "captureUnityLog", true);
                _captureBepInEx = ReadBool(values, "captureBepInExLog", true);
                _capturePlayer = ReadBool(values, "capturePlayerLog", true);
                _captureFps = ReadBool(values, "captureFps", false);
                _captureStack = ReadBool(values, "captureStackTrace", true);
                _captureCheckpoints = ReadBool(values, "captureCheckpoints", true);
                _level = ReadString(values, "level", "info").ToLowerInvariant();
                _maxQueue = Math.Max(64, ReadInt(values, "maxQueue", 512));
                _maxSessions = Math.Max(1, ReadInt(values, "maxLogSessions", 21));
                _dedupeSeconds = Math.Max(0, ReadInt(values, "deduplicateSeconds", 5));
                _suppressNativeSpam = ReadBool(values, "suppressNativeSpam", true);
                _maxPlayerPromptsPerPattern = Math.Max(1, ReadInt(values, "maxPlayerPromptsPerPattern", 20));
                _infoSpamSummarySeconds = Math.Max(1, ReadInt(values, "infoSpamSummarySeconds", 30));
                if (_level != "debug" && _level != "info" && _level != "warning") _level = "info";
            }
            catch { _level = "info"; }
        }

        private static bool ReadBool(Dictionary<string, string> values, string key, bool fallback)
        {
            string value;
            return values.TryGetValue(key, out value) ? string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1" : fallback;
        }

        private static int ReadInt(Dictionary<string, string> values, string key, int fallback)
        {
            string value;
            int result;
            return values.TryGetValue(key, out value) && int.TryParse(value, out result) ? result : fallback;
        }

        private static string ReadString(Dictionary<string, string> values, string key, string fallback)
        {
            string value;
            return values.TryGetValue(key, out value) && !string.IsNullOrEmpty(value) ? value : fallback;
        }

        private string CreateSessionDirectory(string root)
        {
            var logs = Path.Combine(root, "logs");
            Directory.CreateDirectory(logs);
            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var session = Path.Combine(logs, "logs_info_" + stamp);
            var suffix = 0;
            while (Directory.Exists(session)) session = Path.Combine(logs, "logs_info_" + stamp + "_" + (++suffix));
            Directory.CreateDirectory(session);
            var dirs = new List<string>(Directory.GetDirectories(logs, "logs_*"));
            dirs.Sort(StringComparer.OrdinalIgnoreCase);
            while (dirs.Count > _maxSessions)
            {
                try { Directory.Delete(dirs[0], true); } catch { }
                dirs.RemoveAt(0);
            }
            return session;
        }

        private static string Header(string kind)
        {
            return "# OC2DIYLevel " + kind + " log\n# 启动时间: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        }

        private static string Stamp(string level, string source, string message)
        {
            return "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "][" + level + "][" + source + "] " + (message ?? "");
        }

        private static void Append(string path, List<string> lines)
        {
            using (var writer = new StreamWriter(path, true))
                for (int i = 0; i < lines.Count; i++) writer.WriteLine(lines[i]);
        }

        private static void WriteLine(string path, string line)
        {
            using (var writer = new StreamWriter(path, true)) writer.WriteLine(line);
        }

        private static string DefaultConfig()
        {
            return "# 是否启用独立日志插件\nenabled=true\n\n# 最高保存等级：warning / info / debug\nlevel=info\n\n# 是否捕捉 Unity 日志\ncaptureUnityLog=true\n\n# 是否捕捉 BepInEx 日志\ncaptureBepInExLog=true\n\n# 是否保存 Unity 堆栈\ncaptureStackTrace=true\n\n# 是否保存玩家可读的重大错误\ncapturePlayerLog=true\n\n# 是否记录 FPS\ncaptureFps=false\n\n# 是否记录 debug 阶段断点\ncaptureCheckpoints=true\n\n# 内存队列最大条数\nmaxQueue=512\n\n# 最多保留的启动日志会话数\nmaxLogSessions=21\n\n# 相同消息去重秒数\ndeduplicateSeconds=5\n\n# 原生良性错误不写 player.log，只留首条样本+汇总\nsuppressNativeSpam=true\n\n# 同一模式错误最多逐条提示玩家的次数（超出后只汇总）\nmaxPlayerPromptsPerPattern=20\n\n# 重复错误汇总间隔（秒）\ninfoSpamSummarySeconds=30\n";
        }

        private sealed class BepInExListener : ILogListener
        {
            private readonly DebugLogPlugin _owner;
            internal BepInExListener(DebugLogPlugin owner) { _owner = owner; }
            public void LogEvent(object sender, LogEventArgs eventArgs) { _owner.OnBepInExLog(eventArgs); }
            public void Dispose() { }
        }
    }
}
