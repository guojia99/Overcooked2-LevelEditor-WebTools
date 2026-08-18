using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Play 模式日志自动捕获：进入 Play 后把 Console 全部输出（含堆栈）实时写入
/// 仓库根 logs/playmode_yyyyMMdd_HHmmss.log，方便事后排查「缺失脚本 /
/// Animator 警告刷屏 / Play 卡死」之类问题。
///
/// 设计要点：
///   - 进入 Play 会发生 domain reload，静态状态被清空。会话文件路径存入
///     EditorPrefs，reload 后的新域在静态构造里检测到 isPlayingOrWillChangePlaymode
///     即恢复对同一文件的捕获，避免 Play 期间日志丢失；
///   - logMessageReceived 可能在任意线程触发，缓冲统一加锁；
///   - 刷屏防护：同一消息（type+文本）最多写 MaxIdenticalMessages 条，超出后
///     只记一条「后续省略」标记，避免 Animator 每帧警告把磁盘打爆；
///   - 每 0.5s（或缓冲超 64KB）落盘一次；退出 Play / 回到编辑模式时强制落盘。
/// 菜单：Layout Editor → Play 模式日志捕获（写入 logs/） 可开关，默认开启。
/// </summary>
[InitializeOnLoad]
public static class LayoutEditorPlayModeLogCapture
{
    private const string EnabledPrefKey = "LayoutEditor.PlayModeLogCapture.Enabled";
    private const string SessionFilePrefKey = "LayoutEditor.PlayModeLogCapture.SessionFile";
    private const string ToggleMenuPath = "Layout Editor/Play 模式日志捕获（写入 logs/）";
    private const int MaxIdenticalMessages = 50;
    private const double FlushIntervalSeconds = 0.5;
    private const int FlushBufferChars = 64 * 1024;

    private static readonly object Sync = new object();
    private static readonly StringBuilder Buffer = new StringBuilder();
    private static readonly Dictionary<string, int> MessageCounts = new Dictionary<string, int>();

    private static bool _capturing;
    private static string _filePath;
    private static double _lastFlushTime;

    static LayoutEditorPlayModeLogCapture()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        // Play 域重建后静态构造会再次执行：若仍处于（或即将进入）Play 且
        // 已有会话文件，则恢复捕获，覆盖 reload 边界。
        if (IsEnabled() && EditorApplication.isPlayingOrWillChangePlaymode)
        {
            var existing = EditorPrefs.GetString(SessionFilePrefKey, null);
            if (!string.IsNullOrEmpty(existing))
                StartCapture(existing, false);
        }
    }

    [MenuItem(ToggleMenuPath, false, 301)]
    private static void ToggleEnabled()
    {
        var enabled = !IsEnabled();
        EditorPrefs.SetBool(EnabledPrefKey, enabled);
        if (!enabled && _capturing)
            StopCapture("—— 日志捕获被手动关闭 ——");
    }

    [MenuItem(ToggleMenuPath, true)]
    private static bool ToggleEnabledValidate()
    {
        Menu.SetChecked(ToggleMenuPath, IsEnabled());
        return true;
    }

    [MenuItem("Layout Editor/打开日志目录", false, 302)]
    private static void OpenLogsDir()
    {
        var dir = LogsDir();
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        EditorUtility.RevealInFinder(dir);
    }

    private static bool IsEnabled()
    {
        return EditorPrefs.GetBool(EnabledPrefKey, true);
    }

    private static string LogsDir()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "../logs"));
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            if (!IsEnabled())
                return;
            var path = NewSessionFile();
            EditorPrefs.SetString(SessionFilePrefKey, path);
            StartCapture(path, true);
        }
        else if (state == PlayModeStateChange.EnteredPlayMode)
        {
            // 未开 domain reload 的路径（静态构造已覆盖 reload 情形）
            if (!IsEnabled() || _capturing)
                return;
            var existing = EditorPrefs.GetString(SessionFilePrefKey, null);
            if (!string.IsNullOrEmpty(existing))
                StartCapture(existing, false);
        }
        else if (state == PlayModeStateChange.ExitingPlayMode)
        {
            // domain reload 之前尽量把残余缓冲落盘
            if (_capturing)
            {
                AppendLine("—— 退出 Play 模式 ——");
                Flush();
            }
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorPrefs.DeleteKey(SessionFilePrefKey);
            StopCapture("—— 回到编辑模式，日志捕获结束 ——");
        }
    }

    private static string NewSessionFile()
    {
        var dir = LogsDir();
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        return Path.Combine(dir, "playmode_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".log");
    }

    private static void StartCapture(string path, bool writeHeader)
    {
        if (_capturing)
            return;
        _capturing = true;
        _filePath = path;
        _lastFlushTime = EditorApplication.timeSinceStartup;
        Application.logMessageReceived += OnLogMessage;
        EditorApplication.update += OnEditorUpdate;
        if (writeHeader)
        {
            var scene = EditorSceneManagerGetActiveScenePath();
            AppendLine("======== Play 模式日志捕获 ========");
            AppendLine("时间: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            AppendLine("Unity: " + Application.unityVersion);
            AppendLine("场景: " + (string.IsNullOrEmpty(scene) ? "(未保存)" : scene));
            AppendLine("==================================");
        }
        else
        {
            AppendLine("—— （domain reload，捕获继续） ——");
        }
        Flush();
    }

    private static void StopCapture(string footer)
    {
        if (!_capturing)
            return;
        AppendLine(footer);
        Flush();
        Application.logMessageReceived -= OnLogMessage;
        EditorApplication.update -= OnEditorUpdate;
        _capturing = false;
        _filePath = null;
        lock (Sync)
        {
            MessageCounts.Clear();
        }
    }

    private static string EditorSceneManagerGetActiveScenePath()
    {
        try
        {
            return UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static void OnLogMessage(string condition, string stackTrace, LogType type)
    {
        try
        {
            lock (Sync)
            {
                var key = type.ToString() + "|" + condition;
                int count;
                MessageCounts.TryGetValue(key, out count);
                count++;
                MessageCounts[key] = count;
                if (count > MaxIdenticalMessages)
                {
                    if (count == MaxIdenticalMessages + 1)
                    {
                        Buffer.Append(DateTime.Now.ToString("HH:mm:ss.fff"))
                              .Append(" [").Append(type.ToString()).Append("] （相同消息已达 ")
                              .Append(MaxIdenticalMessages).Append(" 条，后续省略）\n");
                    }
                    return;
                }
                Buffer.Append(FormatLine(condition, stackTrace, type)).Append('\n');
            }
        }
        catch (Exception)
        {
            // 日志捕获绝不能影响编辑器/运行流程
        }
    }

    private static string FormatLine(string condition, string stackTrace, LogType type)
    {
        var sb = new StringBuilder();
        sb.Append(DateTime.Now.ToString("HH:mm:ss.fff"));
        sb.Append(" [").Append(type.ToString()).Append("] ");
        sb.Append(condition);
        if (type != LogType.Log && !string.IsNullOrEmpty(stackTrace))
            sb.Append('\n').Append(stackTrace);
        return sb.ToString();
    }

    private static void AppendLine(string line)
    {
        lock (Sync)
        {
            Buffer.Append(line).Append('\n');
        }
    }

    private static void OnEditorUpdate()
    {
        if (!_capturing)
            return;
        var now = EditorApplication.timeSinceStartup;
        bool due;
        lock (Sync)
        {
            due = Buffer.Length > 0 &&
                  (now - _lastFlushTime >= FlushIntervalSeconds || Buffer.Length >= FlushBufferChars);
        }
        if (due)
        {
            _lastFlushTime = now;
            Flush();
        }
    }

    private static void Flush()
    {
        string text = null;
        lock (Sync)
        {
            if (Buffer.Length == 0 || _filePath == null)
                return;
            text = Buffer.ToString();
            Buffer.Length = 0;
        }
        try
        {
            File.AppendAllText(_filePath, text);
        }
        catch (Exception)
        {
            // 磁盘写失败不影响编辑器
        }
    }
}
