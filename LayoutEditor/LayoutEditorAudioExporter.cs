using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using LevelEditorStub;
using UnityEditor;
using UnityEngine;

public static class LayoutEditorAudioExporter
{
    private struct AudioAssetInfo
    {
        public string guid;
        public string id;
        public string bundleName;
        public string assetPath; // in-bundle path
        public string nameZh;
    }

    // ==================== Audio export request (Unity -> python) ====================

    [Serializable]
    public class AudioExportRequestClip
    {
        public string kind;   // "oneshot" | "looping"
        public int entry;     // index into OneShotAudio / LoopingAudio
        public string part;   // "" | "start" | "end"
        public string tag;
        public string type;   // "oneshot", "looping", "looping_start", "looping_end"
        public string filename;
    }

    [Serializable]
    public class AudioExportRequestDir
    {
        public string bundle;
        public string container;
        public string dirId;
        public string nameZh;
        public AudioExportRequestClip[] clips;
    }

    [Serializable]
    public class AudioExportRequestMusic
    {
        public string bundle;
        public string container;
        public string guid;
        public string id;
        public string nameZh;
        public string filename;
    }

    [Serializable]
    public class AudioExportRequestAmbience
    {
        public string tag;
        public string filename;
        public string dirId;
    }

    [Serializable]
    public class AudioExportRequest
    {
        public string exportRoot;
        public string bundlesDir;
        public AudioExportRequestMusic[] music;
        public AudioExportRequestDir[] dirs;
        public AudioExportRequestAmbience[] ambiences;
    }

    // ==================== Audio export result (python -> Unity) ====================

    [Serializable]
    public class AudioExportResultFile
    {
        public string key;
        public bool ok;
        public string actual;
        public string error;
    }

    [Serializable]
    public class AudioExportResult
    {
        public int ok;
        public int fail;
        public AudioExportResultFile[] files;
    }

    // ==================== async export state ====================

    private static System.Diagnostics.Process _exportProcess;
    private static AudioExportRequest _exportRequest;
    private static string _exportRoot;
    private static float _exportStartTime;

    public static void ExportAudioForWeb()
    {
        if (_exportProcess != null && !_exportProcess.HasExited)
        {
            EditorUtility.DisplayDialog("Export Audio", "上一次导出仍在进行中，请稍候。", "OK");
            return;
        }

        string exportRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../audio-exports"));
        try
        {
            // ---- Phase 1: Scan ----
            EditorUtility.DisplayProgressBar("Export Audio", "扫描 BGM 资源…", 0.02f);
            var musicAssets = ScanAudioAssets("audio/music");
            EditorUtility.DisplayProgressBar("Export Audio", "扫描音效集资源…", 0.05f);
            var dirAssets = ScanAudioAssets("audio/AudioDirectories");

            // ---- Phase 2: Load bundles (for reading AudioDirectoryData entries) ----
            var bundleNames = new HashSet<string>();
            foreach (var a in musicAssets) if (!string.IsNullOrEmpty(a.bundleName)) bundleNames.Add(a.bundleName);
            foreach (var a in dirAssets) if (!string.IsNullOrEmpty(a.bundleName)) bundleNames.Add(a.bundleName);
            var bundles = LoadAllBundles(bundleNames);

            // ---- Phase 3: Build export request ----
            var musicList = new List<AudioExportRequestMusic>();
            for (int i = 0; i < musicAssets.Count; i++)
            {
                var a = musicAssets[i];
                float p = 0.10f + 0.20f * ((float)i / Mathf.Max(1, musicAssets.Count));
                if (EditorUtility.DisplayCancelableProgressBar("Export Audio",
                    "读取 BGM " + (i + 1) + "/" + musicAssets.Count + ": " + a.nameZh, p))
                    return;
                musicList.Add(new AudioExportRequestMusic
                {
                    bundle = a.bundleName,
                    container = a.assetPath,
                    guid = a.guid,
                    id = a.id,
                    nameZh = a.nameZh,
                    filename = "bgm/" + SanitizeFilename(a.id) + ".ogg"
                });
            }

            var sfxList = new List<AudioExportRequestDir>();
            var ambienceCandidates = new Dictionary<string, AudioExportRequestAmbience>();
            for (int i = 0; i < dirAssets.Count; i++)
            {
                var a = dirAssets[i];
                float p = 0.30f + 0.40f * ((float)i / Mathf.Max(1, dirAssets.Count));
                if (EditorUtility.DisplayCancelableProgressBar("Export Audio",
                    "读取音效集 " + (i + 1) + "/" + dirAssets.Count + ": " + a.nameZh, p))
                    return;

                AssetBundle bundle2;
                if (!bundles.TryGetValue(a.bundleName, out bundle2))
                    continue;

                var dirData = bundle2.LoadAsset<AudioDirectoryData>(a.assetPath);
                if (dirData == null)
                {
                    Debug.LogWarning("Export Audio: AudioDirectoryData is null — " + a.id);
                    sfxList.Add(new AudioExportRequestDir
                        { dirId = a.id, nameZh = a.nameZh, clips = new AudioExportRequestClip[0] });
                    continue;
                }

                var clips = new List<AudioExportRequestClip>();

                // OneShotAudio
                if (dirData.OneShotAudio != null)
                {
                    for (int e = 0; e < dirData.OneShotAudio.Length; e++)
                    {
                        var entry = dirData.OneShotAudio[e];
                        var clip = GetBaseAudioClip(entry);
                        if (clip == null) continue;
                        string tag = entry.Tag.ToString();
                        string fn = "sfx/" + SanitizeFilename(a.id) + "/oneshot_" + SanitizeFilename(tag) + ".ogg";
                        clips.Add(new AudioExportRequestClip
                            { kind = "oneshot", entry = e, part = "", tag = tag, type = "oneshot", filename = fn });
                    }
                }

                // LoopingAudio
                if (dirData.LoopingAudio != null)
                {
                    for (int e = 0; e < dirData.LoopingAudio.Length; e++)
                    {
                        var entry = dirData.LoopingAudio[e];
                        string tag = entry.Tag.ToString();

                        var baseClip = GetBaseAudioClip(entry);
                        if (baseClip != null)
                        {
                            string fn = "sfx/" + SanitizeFilename(a.id) + "/looping_" + SanitizeFilename(tag) + ".ogg";
                            clips.Add(new AudioExportRequestClip
                                { kind = "looping", entry = e, part = "base", tag = tag, type = "looping", filename = fn });
                            if (!ambienceCandidates.ContainsKey(tag))
                                ambienceCandidates[tag] = new AudioExportRequestAmbience
                                    { tag = tag, filename = fn, dirId = a.id };
                        }

                        if (entry.StartClip != null)
                        {
                            string fn = "sfx/" + SanitizeFilename(a.id) + "/looping_" + SanitizeFilename(tag) + "_start.ogg";
                            clips.Add(new AudioExportRequestClip
                                { kind = "looping", entry = e, part = "start", tag = tag, type = "looping_start", filename = fn });
                        }

                        if (entry.EndClip != null)
                        {
                            string fn = "sfx/" + SanitizeFilename(a.id) + "/looping_" + SanitizeFilename(tag) + "_end.ogg";
                            clips.Add(new AudioExportRequestClip
                                { kind = "looping", entry = e, part = "end", tag = tag, type = "looping_end", filename = fn });
                        }
                    }
                }

                sfxList.Add(new AudioExportRequestDir
                {
                    bundle = a.bundleName,
                    container = a.assetPath,
                    dirId = a.id,
                    nameZh = a.nameZh,
                    clips = clips.ToArray()
                });
            }

            var ambList = new List<AudioExportRequestAmbience>();
            foreach (var kv in ambienceCandidates) ambList.Add(kv.Value);

            var request = new AudioExportRequest
            {
                exportRoot = exportRoot,
                bundlesDir = Path.Combine(Application.streamingAssetsPath, "Windows").Replace('\\', '/'),
                music = musicList.ToArray(),
                dirs = sfxList.ToArray(),
                ambiences = ambList.ToArray()
            };

            // ---- Phase 4: Clean old exports & write request ----
            EditorUtility.DisplayProgressBar("Export Audio", "准备导出…", 0.75f);
            if (Directory.Exists(exportRoot))
                Directory.Delete(exportRoot, true);
            Directory.CreateDirectory(exportRoot);

            File.WriteAllText(
                Path.Combine(exportRoot, "audio-export-request.json"),
                JsonUtility.ToJson(request, true));

            // ---- Phase 5: Run python extractor ----
            EditorUtility.ClearProgressBar();
            _exportRequest = request;
            _exportRoot = exportRoot;
            StartExportProcess();
        }
        catch (Exception ex)
        {
            EditorUtility.ClearProgressBar();
            Debug.LogError("Export Audio failed: " + ex);
            EditorUtility.DisplayDialog("Export Audio Error", ex.Message, "OK");
        }
    }

    private static void StartExportProcess()
    {
        string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string scriptPath = Path.Combine(Path.Combine(Path.Combine(repoRoot, "layout-editor"), "scripts"), "export-audio.py");
        string reqPath = Path.Combine(_exportRoot, "audio-export-request.json");

        if (!File.Exists(scriptPath))
        {
            _exportRequest = null;
            EditorUtility.DisplayDialog("Export Audio",
                "未找到导出脚本：\n" + scriptPath + "\n\n请确认 layout-editor 目录完整。", "OK");
            return;
        }

        var psi = new System.Diagnostics.ProcessStartInfo();
        psi.FileName = "python3";
        psi.Arguments = "\"" + scriptPath + "\" \"" + reqPath + "\"";
        psi.UseShellExecute = false;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.CreateNoWindow = true;

        try
        {
            _exportProcess = System.Diagnostics.Process.Start(psi);
        }
        catch (Exception ex)
        {
            // fallback to "python"
            try
            {
                psi.FileName = "python";
                _exportProcess = System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex2)
            {
                _exportRequest = null;
                EditorUtility.DisplayDialog("Export Audio",
                    "无法启动 python 导出器（" + ex2.Message + "）。\n\n请安装 Python3 后重试。", "OK");
                return;
            }
        }

        _exportStartTime = Time.realtimeSinceStartup;
        EditorApplication.update -= ExportAudioTick;
        EditorApplication.update += ExportAudioTick;
    }

    private static void ExportAudioTick()
    {
        if (_exportProcess == null) return;
        if (!_exportProcess.HasExited)
        {
            float secs = Time.realtimeSinceStartup - _exportStartTime;
            EditorUtility.DisplayProgressBar("Export Audio",
                "正在提取音频… " + (int)secs + "s（首次运行会自动安装 Python 依赖）", 0.5f);
            return;
        }

        EditorApplication.update -= ExportAudioTick;
        EditorUtility.ClearProgressBar();

        string stdout = _exportProcess.StandardOutput.ReadToEnd();
        string stderr = _exportProcess.StandardError.ReadToEnd();
        int exitCode = _exportProcess.ExitCode;
        _exportProcess = null;

        if (!string.IsNullOrEmpty(stdout))
            Debug.Log("Export Audio (python):\n" + stdout);
        if (!string.IsNullOrEmpty(stderr))
            Debug.LogWarning("Export Audio (python stderr):\n" + stderr);

        try
        {
            FinalizeExport(exitCode, stdout);
        }
        catch (Exception ex)
        {
            Debug.LogError("Export Audio finalize failed: " + ex);
            EditorUtility.DisplayDialog("Export Audio Error", ex.Message, "OK");
        }
    }

    private static void FinalizeExport(int exitCode, string pythonOutput)
    {
        var results = new Dictionary<string, AudioExportResultFile>();
        string resultPath = Path.Combine(_exportRoot, "audio-export-result.json");
        if (File.Exists(resultPath))
        {
            try
            {
                var r = JsonUtility.FromJson<AudioExportResult>(File.ReadAllText(resultPath));
                if (r.files != null)
                    foreach (var f in r.files)
                        if (f != null && !string.IsNullOrEmpty(f.key))
                            results[f.key] = f;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Export Audio: parse result failed: " + ex.Message);
            }
        }

        int okCount = 0;
        int failCount = 0;
        var req = _exportRequest;
        _exportRequest = null;

        // ---- BGM ----
        var bgmList = new List<AudioExportBgmDto>();
        foreach (var m in req.music ?? new AudioExportRequestMusic[0])
        {
            AudioExportResultFile f;
            if (results.TryGetValue(m.filename, out f) && f.ok && !string.IsNullOrEmpty(f.actual))
            {
                bgmList.Add(new AudioExportBgmDto { guid = m.guid, id = m.id, nameZh = m.nameZh, filename = f.actual });
                okCount++;
            }
            else
            {
                failCount++;
            }
        }

        // ---- SFX dirs ----
        var sfxList = new List<AudioExportSfxDirDto>();
        foreach (var d in req.dirs ?? new AudioExportRequestDir[0])
        {
            var clips = new List<AudioExportSfxClipDto>();
            foreach (var c in d.clips ?? new AudioExportRequestClip[0])
            {
                AudioExportResultFile f;
                if (results.TryGetValue(c.filename, out f) && f.ok && !string.IsNullOrEmpty(f.actual))
                {
                    clips.Add(new AudioExportSfxClipDto { tag = c.tag, type = c.type, filename = f.actual });
                    okCount++;
                }
                else
                {
                    failCount++;
                }
            }
            sfxList.Add(new AudioExportSfxDirDto { id = d.dirId, nameZh = d.nameZh, clips = clips.ToArray() });
        }

        // ---- Ambiences ----
        var ambMap = new Dictionary<string, AudioExportRequestAmbience>();
        foreach (var a in req.ambiences ?? new AudioExportRequestAmbience[0])
            if (!ambMap.ContainsKey(a.tag)) ambMap[a.tag] = a;

        var ambList = new List<AudioExportAmbienceDto>();
        foreach (var tagName in Enum.GetNames(typeof(LevelInfoSO.GameLoopingAudioTag)))
        {
            if (tagName == "COUNT") continue;
            AudioExportRequestAmbience cand;
            AudioExportResultFile f;
            if (ambMap.TryGetValue(tagName, out cand) &&
                results.TryGetValue(cand.filename, out f) && f.ok && !string.IsNullOrEmpty(f.actual))
            {
                ambList.Add(new AudioExportAmbienceDto
                    { tag = tagName, found = true, filename = f.actual, dirId = cand.dirId });
            }
            else
            {
                ambList.Add(new AudioExportAmbienceDto { tag = tagName, found = false });
            }
        }

        var manifest = new AudioExportManifestDto
        {
            generatedAt = DateTime.UtcNow.ToString("o"),
            bgm = bgmList.ToArray(),
            sfx = sfxList.ToArray(),
            ambiences = ambList.ToArray()
        };
        File.WriteAllText(
            Path.Combine(_exportRoot, "audio-exports.json"),
            JsonUtility.ToJson(manifest, true));

        string msg = string.Format("导出完成！\n\n成功 {0} 个，失败 {1} 个。\n\n输出目录：\n{2}",
            okCount, failCount, _exportRoot);
        if (exitCode != 0)
            msg += "\n\n⚠️ 导出器脚本异常退出，失败原因见 Console 日志。";
        else if (!string.IsNullOrEmpty(pythonOutput) && pythonOutput.Contains("ERROR:"))
            msg += "\n\n⚠️ 导出器报告错误，详见 Console 日志。";
        EditorUtility.DisplayDialog("Export Audio", msg, "OK");
    }

    // -------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------

    private static List<AudioAssetInfo> ScanAudioAssets(string subFolder)
    {
        var roots = new[]
        {
            "Assets/common01/pseudo_prefab_so/" + subFolder,
            "Assets/common02/pseudo_prefab_so/" + subFolder
        };
        var list = new List<AudioAssetInfo>();
        var seen = new HashSet<string>();

        foreach (var root in roots)
        {
            if (!AssetDatabase.IsValidFolder(root))
                continue;
            foreach (var guid in AssetDatabase.FindAssets("t:PseudoPrefabSO", new[] { root }))
            {
                if (!seen.Add(guid))
                    continue;
                var soPath = AssetDatabase.GUIDToAssetPath(guid);
                var so = AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(soPath);
                if (so == null || string.IsNullOrEmpty(so.bundleName))
                    continue;

                var id = Path.GetFileNameWithoutExtension(soPath);
                string nameZh;
                string nameEn;
                LayoutEditorManualLookup.TryGet(id, out nameZh, out nameEn);
                if (string.IsNullOrEmpty(nameZh))
                    nameZh = so.prefabName ?? id;

                list.Add(new AudioAssetInfo
                {
                    guid = guid,
                    id = id,
                    bundleName = so.bundleName,
                    assetPath = so.assetPath,
                    nameZh = nameZh
                });
            }
        }

        list.Sort((a, b) => string.Compare(a.id, b.id, StringComparison.Ordinal));
        return list;
    }

    private static bool _bundlesReused;

    private static Dictionary<string, AssetBundle> LoadAllBundles(HashSet<string> bundleNames)
    {
        // Reuse bundles already loaded by PseudoPrefabManager when a scene is open
        var mgr = LevelEditor.PseudoPrefabManager.Instance;
        if (mgr != null)
        {
            var reused = new Dictionary<string, AssetBundle>();
            foreach (var name in bundleNames)
            {
                try
                {
                    var bundle = LevelEditor.PseudoPrefabManager.GetAssetBundle(name);
                    if (bundle != null)
                        reused[name] = bundle;
                }
                catch (System.Exception)
                {
                    // bundle not in manager's dict, will be missing
                }
            }
            if (reused.Count > 0)
            {
                _bundlesReused = true;
                return reused;
            }
        }

        _bundlesReused = false;
        string manifestPath = Path.Combine(Application.streamingAssetsPath, "Windows/Windows").Replace('\\', '/');
        var manifestBundle = AssetBundle.LoadFromFile(manifestPath);
        if (manifestBundle == null)
            throw new System.Exception("Cannot load Windows manifest bundle from " + manifestPath);

        var manifest = manifestBundle.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
        if (manifest == null)
            throw new System.Exception("AssetBundleManifest not found in Windows bundle");

        var allNames = new HashSet<string>(bundleNames);
        foreach (var name in bundleNames)
        foreach (var dep in manifest.GetAllDependencies(name))
            allNames.Add(dep);

        var bundles = new Dictionary<string, AssetBundle>();
        foreach (var name in allNames)
        {
            string path = Path.Combine(Application.streamingAssetsPath, "Windows/" + name)
                .Replace('\\', '/');
            if (!File.Exists(path))
                continue;
            var bundle = AssetBundle.LoadFromFile(path);
            if (bundle != null)
                bundles[name] = bundle;
        }

        manifestBundle.Unload(false);
        return bundles;
    }

    private static AudioClip GetBaseAudioClip(object entry)
    {
        var type = entry.GetType();
        foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (f.FieldType != typeof(AudioClip))
                continue;
            if (f.Name == "StartClip" || f.Name == "EndClip")
                continue;
            return f.GetValue(entry) as AudioClip;
        }

        return null;
    }

    private static string SanitizeFilename(string s)
    {
        if (string.IsNullOrEmpty(s)) return "_";
        var invalid = Path.GetInvalidFileNameChars();
        var chars = s.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
            if (Array.IndexOf(invalid, chars[i]) >= 0)
                chars[i] = '_';
        return new string(chars);
    }
}
