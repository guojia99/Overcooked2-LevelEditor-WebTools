using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Console 日志常驻捕获：编辑器启动后（编辑模式 + Play 全程）把 Console 全部
/// 输出（含堆栈）实时写入仓库根 logs/console_log.log，方便事后排查定位。
///
/// 设计要点：
///   - [InitializeOnLoad] 静态构造即订阅，domain reload 后自动重新订阅，与
///     Play 状态无关，天然常驻；
///   - logMessageReceived 可能在任意线程触发，缓冲统一加锁；
///   - 刷屏防护：同一消息（type+文本）最多写 MaxIdenticalMessages 条；
///   - 每 0.5s（或缓冲超 64KB）落盘一次；
///   - 滚动限制：文件超过 MaxFileBytes（32MB）时截掉前面 TrimBytes（4MB），
///     保留尾部，并在截断处写入带时间戳的分隔行。
/// 菜单：Layout Editor → 诊断 (Diagnostics) → Console 日志捕获 (Console Log
/// Capture) 可开关，默认开启。
/// </summary>
[InitializeOnLoad]
public static class LayoutEditorConsoleLogCapture
{
    private const string EnabledPrefKey = "LayoutEditor.ConsoleLogCapture.Enabled";
    private const string ToggleMenuPath = "Layout Editor/诊断 (Diagnostics)/Console 日志捕获 (Console Log Capture)";
    private const int MaxIdenticalMessages = 50;
    private const double FlushIntervalSeconds = 0.5;
    private const int FlushBufferChars = 64 * 1024;
    private const long MaxFileBytes = 32L * 1024 * 1024;
    private const long TrimBytes = 4L * 1024 * 1024;

    private static readonly object Sync = new object();
    private static readonly StringBuilder Buffer = new StringBuilder();
    private static readonly Dictionary<string, int> MessageCounts = new Dictionary<string, int>();

    private static string _filePath;
    private static double _lastFlushTime;
    private static bool _subscribed;

    static LayoutEditorConsoleLogCapture()
    {
        if (IsEnabled())
            Subscribe();
    }

    private static void Subscribe()
    {
        if (_subscribed)
            return;
        _subscribed = true;
        _filePath = ConsoleLogPath();
        _lastFlushTime = EditorApplication.timeSinceStartup;
        Application.logMessageReceived += OnLogMessage;
        EditorApplication.update += OnEditorUpdate;
    }

    private static void Unsubscribe()
    {
        if (!_subscribed)
            return;
        Flush();
        Application.logMessageReceived -= OnLogMessage;
        EditorApplication.update -= OnEditorUpdate;
        _subscribed = false;
        _filePath = null;
        lock (Sync)
        {
            MessageCounts.Clear();
        }
    }

    [MenuItem(ToggleMenuPath, false, 2)]
    private static void ToggleEnabled()
    {
        var enabled = !IsEnabled();
        EditorPrefs.SetBool(EnabledPrefKey, enabled);
        if (enabled)
            Subscribe();
        else
            Unsubscribe();
    }

    [MenuItem(ToggleMenuPath, true)]
    private static bool ToggleEnabledValidate()
    {
        Menu.SetChecked(ToggleMenuPath, IsEnabled());
        return true;
    }

    private static bool IsEnabled()
    {
        return EditorPrefs.GetBool(EnabledPrefKey, true);
    }

    private static string ConsoleLogPath()
    {
        try
        {
            var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../logs"));
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            return Path.Combine(dir, "console_log.log");
        }
        catch (Exception)
        {
            return "console_log.log";
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

    private static void OnEditorUpdate()
    {
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
            RollIfNeeded();
        }
        catch (Exception)
        {
            // 磁盘写失败不影响编辑器
        }
    }

    /// <summary>超过 32MB 时截掉前 4MB（保留尾部 28MB），并在新头部写时间戳分隔行。</summary>
    private static void RollIfNeeded()
    {
        var info = new FileInfo(_filePath);
        if (!info.Exists || info.Length <= MaxFileBytes)
            return;
        var all = File.ReadAllText(_filePath);
        // 按文件实际字节数换算 4MB 对应的字符数（UTF-8 中文多字节，不能按 1:1 估）
        var cut = (int)(all.Length * TrimBytes / info.Length);
        if (cut <= 0 || cut >= all.Length)
            return;
        // 对齐到换行，避免截出半行
        var nl = all.IndexOf('\n', cut);
        if (nl > 0 && nl < all.Length - 1)
            cut = nl + 1;
        var tail = all.Substring(cut);
        var marker = "—— console_log 滚动截断 @ " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                     + "，已丢弃前 4MB（文件超 32MB） ——\n";
        File.WriteAllText(_filePath, marker + tail);
    }
}
