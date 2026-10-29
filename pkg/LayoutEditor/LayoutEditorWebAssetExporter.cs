using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Web 前端资源统一导出（音频 / 桌台图标 / 桌台 3D / 装饰双视图）。
///
/// 产物全部写入 &lt;repo&gt;/exports/（git 忽略），由 LayoutEditorHttpServer 的
/// /api/web-assets 路由只读提供；不再写入 layout-editor/web/public。
///
/// 长任务：StartExport 立即返回，EditorApplication.delayCall 在主线程串行执行；
/// HTTP 状态查询走监听线程 fast-path（只读锁保护的状态快照）。
/// </summary>
public static class LayoutEditorWebAssetExporter
{
    public const string CategoryAudio = "audio";
    public const string CategoryCounterIcons = "counter-icons";
    public const string CategoryCounter3d = "counter-3d";
    public const string CategoryDecor = "decor";

    [MenuItem("Layout Editor/素材导出 (Asset Export)/导出全部 Web 依赖 (Audio + Counter + Decor)", false, 4)]
    public static void ExportAllMenu()
    {
        string err = StartExport(false, null);
        if (!string.IsNullOrEmpty(err))
        {
            EditorUtility.DisplayDialog("Web 资源导出", err, "OK");
            return;
        }
        EditorUtility.DisplayDialog("Web 资源导出", "已开始导出音频 / 桌台 / 装饰双视图。\n进度请在 Web 关卡管理页「导出 Web 资源」弹窗查看。", "OK");
    }

    [MenuItem("Layout Editor/素材导出 (Asset Export)/强制重导全部 Web 依赖 (Re-export All)", false, 5)]
    public static void ExportAllForceMenu()
    {
        string err = StartExport(true, null);
        if (!string.IsNullOrEmpty(err))
        {
            EditorUtility.DisplayDialog("Web 资源导出", err, "OK");
            return;
        }
        EditorUtility.DisplayDialog("Web 资源导出", "已开始全量重导（装饰正/背视图会全部重渲染）。\n进度请在 Web 关卡管理页查看。", "OK");
    }

    private static readonly object _lock = new object();
    private static WebAssetExportStatusDto _status = NewIdleStatus();

    public static WebAssetExportStatusDto GetStatus()
    {
        lock (_lock)
        {
            return CopyStatus(_status);
        }
    }

    /// <summary>启动一键导出。返回空字符串表示已排队；否则为错误文案。</summary>
    public static string StartExport(bool force, List<string> categories)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return "请先退出 Play 模式再导出 Web 资源。";

        var setExportStatus = LayoutEditorSetExporter.GetStatus();
        if (setExportStatus != null && setExportStatus.status == "running")
            return "关卡集导出正在进行中，请等待完成后再导出 Web 资源。";

        List<string> selected = NormalizeCategories(categories);
        if (selected.Count == 0)
            return "请至少选择一个导出类别。";

        lock (_lock)
        {
            if (_status.status == "running")
                return "已有 Web 资源导出任务正在进行，请稍候。";
            _status = NewRunningStatus(selected, force);
        }

        EditorApplication.delayCall += RunExport;
        return "";
    }

    private static void RunExport()
    {
        List<string> categories;
        bool force;
        lock (_lock)
        {
            categories = new List<string>(_status.categories);
            force = _status.force;
        }

        var errors = new List<string>();
        var summary = new WebAssetExportSummary();
        long startedTicks = DateTime.UtcNow.Ticks;

        try
        {
            for (int i = 0; i < categories.Count; i++)
            {
                string cat = categories[i];
                float baseProgress = 0.02f + 0.94f * ((float)i / Mathf.Max(1, categories.Count));
                float span = 0.94f / Mathf.Max(1, categories.Count);
                if (cat == CategoryAudio)
                    RunAudio(baseProgress, span, summary, errors);
                else if (cat == CategoryCounterIcons)
                    RunCounterIcons(baseProgress, span, summary, errors);
                else if (cat == CategoryCounter3d)
                    RunCounter3d(baseProgress, span, summary, errors);
                else if (cat == CategoryDecor)
                    RunDecor(force, baseProgress, span, summary, errors);
            }

            lock (_lock)
            {
                _status.status = errors.Count == 0 ? "done" : "error";
                _status.phase = "done";
                _status.progress = 1f;
                _status.message = errors.Count == 0 ? "Web 资源导出完成。" : "Web 资源导出完成，但有失败项。";
                _status.error = errors.Count == 0 ? "" : JoinErrors(errors);
                _status.finishedAt = DateTime.UtcNow.ToString("o");
                _status.durationMs = (long)((DateTime.UtcNow.Ticks - startedTicks) / TimeSpan.TicksPerMillisecond);
                _status.summary = summary;
            }
            Debug.Log("[WebAssetExport] 完成："
                + "audio=" + summary.audioClips
                + " counterIcons=" + summary.counterIcons
                + " counter3d=" + summary.counter3d
                + " decorFront=" + summary.decorFront
                + " decorBack=" + summary.decorBack
                + (errors.Count == 0 ? "" : " errors=" + JoinErrors(errors)));
        }
        catch (Exception ex)
        {
            Debug.LogError("[WebAssetExport] 导出异常: " + ex);
            lock (_lock)
            {
                _status.status = "error";
                _status.phase = "error";
                _status.message = "导出异常，详见 Unity Console。";
                _status.error = ex.Message;
                _status.finishedAt = DateTime.UtcNow.ToString("o");
                _status.durationMs = (long)((DateTime.UtcNow.Ticks - startedTicks) / TimeSpan.TicksPerMillisecond);
                _status.summary = summary;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static void RunAudio(float baseProgress, float span, WebAssetExportSummary summary, List<string> errors)
    {
        SetPhase("audio", "正在导出音频…", baseProgress);
        var result = LayoutEditorAudioExporter.ExportAudioForWebSilent(delegate(string msg, float p)
        {
            SetProgress("audio", msg, baseProgress + span * Mathf.Clamp01(p));
        });
        summary.audioClips = result.okCount;
        summary.audioFailed = result.failCount;
        if (!string.IsNullOrEmpty(result.fatalError))
            errors.Add("音频：" + result.fatalError);
        else if (result.canceled)
            errors.Add("音频：导出被取消（已写出部分文件）");
    }

    private static void RunCounterIcons(float baseProgress, float span, WebAssetExportSummary summary, List<string> errors)
    {
        SetPhase("counter-icons", "正在导出桌台图标…", baseProgress);
        var result = LayoutEditorCounterSkinIconExport.ExportAllSilent(delegate(string msg, float p)
        {
            SetProgress("counter-icons", msg, baseProgress + span * Mathf.Clamp01(p));
        });
        summary.counterIcons = result.okCount;
        summary.counterIconsFailed = result.failCount;
        if (!string.IsNullOrEmpty(result.fatalError))
            errors.Add("桌台图标：" + result.fatalError);
    }

    private static void RunCounter3d(float baseProgress, float span, WebAssetExportSummary summary, List<string> errors)
    {
        SetPhase("counter-3d", "正在导出桌台 3D 模型…", baseProgress);
        var result = LayoutEditorCounterSkin3dExport.ExportAllSilent(delegate(string msg, float p)
        {
            SetProgress("counter-3d", msg, baseProgress + span * Mathf.Clamp01(p));
        });
        summary.counter3d = result.okCount;
        summary.counter3dFailed = result.failCount;
        if (!string.IsNullOrEmpty(result.fatalError))
            errors.Add("桌台 3D：" + result.fatalError);
    }

    private static void RunDecor(bool force, float baseProgress, float span, WebAssetExportSummary summary, List<string> errors)
    {
        SetPhase("decor", force ? "正在全量重导装饰双视图…" : "正在导出装饰双视图…", baseProgress);
        var result = LayoutEditorDecorThumbnailExporter.ExportSilent(force, delegate(string msg, float p)
        {
            SetProgress("decor", msg, baseProgress + span * Mathf.Clamp01(p));
        });
        summary.decorFront = result.frontTotal;
        summary.decorBack = result.backTotal;
        summary.decorDetailFront = result.detailFrontTotal;
        summary.decorDetailBack = result.detailBackTotal;
        summary.decorModels = result.modelTotal;
        summary.decorMissing = result.missing;
        summary.decorFailed = result.failed;
        if (!string.IsNullOrEmpty(result.fatalError))
            errors.Add("装饰物：" + result.fatalError);
        else if (result.cancelled)
            errors.Add("装饰物：导出被取消（已写出部分文件）");
    }

    private static List<string> NormalizeCategories(List<string> categories)
    {
        var result = new List<string>();
        if (categories == null || categories.Count == 0)
        {
            result.Add(CategoryAudio);
            result.Add(CategoryCounterIcons);
            result.Add(CategoryCounter3d);
            result.Add(CategoryDecor);
            return result;
        }
        for (int i = 0; i < categories.Count; i++)
        {
            string c = (categories[i] ?? "").Trim().ToLowerInvariant();
            if (c.Length == 0 || result.Contains(c)) continue;
            if (c == CategoryAudio || c == CategoryCounterIcons || c == CategoryCounter3d || c == CategoryDecor)
                result.Add(c);
        }
        return result;
    }

    private static string JoinErrors(List<string> errors)
    {
        if (errors == null || errors.Count == 0) return "";
        return string.Join("；", errors.ToArray());
    }

    private static void SetPhase(string phase, string message, float progress)
    {
        lock (_lock)
        {
            _status.phase = phase;
            _status.message = message;
            _status.progress = Mathf.Clamp01(progress);
        }
    }

    private static void SetProgress(string phase, string message, float progress)
    {
        lock (_lock)
        {
            _status.phase = phase;
            _status.message = message;
            _status.progress = Mathf.Clamp01(progress);
        }
    }

    private static WebAssetExportStatusDto NewIdleStatus()
    {
        var st = new WebAssetExportStatusDto();
        st.status = "idle";
        st.phase = "idle";
        st.message = "";
        st.error = "";
        st.categories = new string[0];
        st.summary = new WebAssetExportSummary();
        return st;
    }

    private static WebAssetExportStatusDto NewRunningStatus(List<string> categories, bool force)
    {
        var st = NewIdleStatus();
        st.status = "running";
        st.phase = "starting";
        st.message = "正在启动 Web 资源导出…";
        st.startedAt = DateTime.UtcNow.ToString("o");
        st.categories = categories.ToArray();
        st.force = force;
        return st;
    }

    private static WebAssetExportStatusDto CopyStatus(WebAssetExportStatusDto src)
    {
        var st = new WebAssetExportStatusDto();
        st.status = src.status;
        st.phase = src.phase;
        st.progress = src.progress;
        st.message = src.message;
        st.error = src.error;
        st.startedAt = src.startedAt;
        st.finishedAt = src.finishedAt;
        st.durationMs = src.durationMs;
        st.categories = src.categories == null ? new string[0] : (string[])src.categories.Clone();
        st.force = src.force;
        st.summary = src.summary == null ? new WebAssetExportSummary() : src.summary.Clone();
        return st;
    }
}

[Serializable]
public class WebAssetExportStatusDto
{
    public string status;
    public string phase;
    public float progress;
    public string message;
    public string error;
    public string startedAt;
    public string finishedAt;
    public long durationMs;
    public string[] categories;
    public bool force;
    public WebAssetExportSummary summary;
}

[Serializable]
public class WebAssetExportSummary
{
    public int audioClips;
    public int audioFailed;
    public int counterIcons;
    public int counterIconsFailed;
    public int counter3d;
    public int counter3dFailed;
    public int decorFront;
    public int decorBack;
    public int decorDetailFront;
    public int decorDetailBack;
    public int decorModels;
    public int decorMissing;
    public int decorFailed;

    public WebAssetExportSummary Clone()
    {
        var s = new WebAssetExportSummary();
        s.audioClips = audioClips;
        s.audioFailed = audioFailed;
        s.counterIcons = counterIcons;
        s.counterIconsFailed = counterIconsFailed;
        s.counter3d = counter3d;
        s.counter3dFailed = counter3dFailed;
        s.decorFront = decorFront;
        s.decorBack = decorBack;
        s.decorDetailFront = decorDetailFront;
        s.decorDetailBack = decorDetailBack;
        s.decorModels = decorModels;
        s.decorMissing = decorMissing;
        s.decorFailed = decorFailed;
        return s;
    }
}
