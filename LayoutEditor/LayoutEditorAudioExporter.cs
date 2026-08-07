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

    public static void ExportAudioForWeb()
    {
        string exportRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../audio-exports"));
        Directory.CreateDirectory(Path.Combine(exportRoot, "bgm"));
        Directory.CreateDirectory(Path.Combine(exportRoot, "sfx"));
        Directory.CreateDirectory(Path.Combine(exportRoot, "ambience"));

        try
        {
            // Phase 1: Scan
            EditorUtility.DisplayProgressBar("Export Audio", "Scanning BGM assets...", 0.02f);
            var musicAssets = ScanAudioAssets("audio/music");
            EditorUtility.DisplayProgressBar("Export Audio", "Scanning SFX directories...", 0.05f);
            var dirAssets = ScanAudioAssets("audio/AudioDirectories");

            // Phase 2: Load bundles
            if (EditorUtility.DisplayCancelableProgressBar("Export Audio", "Loading AssetBundles...", 0.08f))
                return;

            var bundleNames = new HashSet<string>();
            foreach (var a in musicAssets) if (!string.IsNullOrEmpty(a.bundleName)) bundleNames.Add(a.bundleName);
            foreach (var a in dirAssets) if (!string.IsNullOrEmpty(a.bundleName)) bundleNames.Add(a.bundleName);
            var bundles = LoadAllBundles(bundleNames);

            // Phase 3: Export BGM (0.10 – 0.40)
            var bgmList = new List<AudioExportBgmDto>();
            int total = musicAssets.Count;
            for (int i = 0; i < total; i++)
            {
                var a = musicAssets[i];
                float p = 0.10f + 0.30f * ((float)i / Mathf.Max(1, total));
                if (EditorUtility.DisplayCancelableProgressBar("Export Audio",
                    "BGM " + (i + 1) + "/" + total + ": " + a.nameZh, p))
                    return;

                AssetBundle bundle;
                if (bundles.TryGetValue(a.bundleName, out bundle))
                {
                    var clip = bundle.LoadAsset<AudioClip>(a.assetPath);
                    if (clip != null)
                    {
                        if (SaveClipAsWav(clip, Path.Combine(Path.Combine(exportRoot, "bgm"), SanitizeFilename(a.id) + ".wav")))
                        {
                            bgmList.Add(new AudioExportBgmDto
                            {
                                guid = a.guid, id = a.id, nameZh = a.nameZh,
                                filename = "bgm/" + SanitizeFilename(a.id) + ".wav"
                            });
                        }
                    }
                    else
                    {
                        Debug.LogWarning("Export Audio: BGM clip is null — " + a.id);
                    }
                }
            }

            // Phase 4: Export SFX + build ambience map (0.40 – 0.75)
            var sfxList = new List<AudioExportSfxDirDto>();
            var ambienceMap = new Dictionary<string, AudioExportAmbienceDto>();
            total = dirAssets.Count;
            for (int i = 0; i < total; i++)
            {
                var a = dirAssets[i];
                float p = 0.40f + 0.35f * ((float)i / Mathf.Max(1, total));
                if (EditorUtility.DisplayCancelableProgressBar("Export Audio",
                    "SFX " + (i + 1) + "/" + total + ": " + a.nameZh, p))
                    return;

                AssetBundle bundle2;
                if (!bundles.TryGetValue(a.bundleName, out bundle2))
                    continue;

                var dirData = bundle2.LoadAsset<AudioDirectoryData>(a.assetPath);
                if (dirData == null)
                {
                    Debug.LogWarning("Export Audio: AudioDirectoryData is null — " + a.id);
                    sfxList.Add(new AudioExportSfxDirDto
                        { id = a.id, nameZh = a.nameZh, clips = new AudioExportSfxClipDto[0] });
                    continue;
                }

                string dirFolder = Path.Combine(Path.Combine(exportRoot, "sfx"), SanitizeFilename(a.id));
                Directory.CreateDirectory(dirFolder);
                var clips = new List<AudioExportSfxClipDto>();

                // OneShotAudio
                if (dirData.OneShotAudio != null)
                {
                    foreach (var entry in dirData.OneShotAudio)
                    {
                        var clip = GetBaseAudioClip(entry);
                        if (clip == null) continue;
                        string tag = entry.Tag.ToString();
                        string fn = "oneshot_" + SanitizeFilename(tag) + ".wav";
                        if (SaveClipAsWav(clip, Path.Combine(dirFolder, fn)))
                        {
                            clips.Add(new AudioExportSfxClipDto
                                { tag = tag, type = "oneshot", filename = "sfx/" + SanitizeFilename(a.id) + "/" + fn });
                        }
                    }
                }

                // LoopingAudio
                if (dirData.LoopingAudio != null)
                {
                    foreach (var entry in dirData.LoopingAudio)
                    {
                        string tag = entry.Tag.ToString();
                        var baseClip = GetBaseAudioClip(entry);

                        if (baseClip != null)
                        {
                            string fn = "looping_" + SanitizeFilename(tag) + ".wav";
                            if (SaveClipAsWav(baseClip, Path.Combine(dirFolder, fn)))
                            {
                                string relPath = "sfx/" + SanitizeFilename(a.id) + "/" + fn;
                                clips.Add(new AudioExportSfxClipDto
                                    { tag = tag, type = "looping", filename = relPath });

                                if (!ambienceMap.ContainsKey(tag))
                                    ambienceMap[tag] = new AudioExportAmbienceDto
                                        { tag = tag, found = true, filename = relPath, dirId = a.id };
                            }
                        }

                        if (entry.StartClip != null)
                        {
                            string fn = "looping_" + SanitizeFilename(tag) + "_start.wav";
                            if (SaveClipAsWav(entry.StartClip, Path.Combine(dirFolder, fn)))
                            {
                                clips.Add(new AudioExportSfxClipDto
                                    { tag = tag, type = "looping_start",
                                        filename = "sfx/" + SanitizeFilename(a.id) + "/" + fn });
                            }
                        }

                        if (entry.EndClip != null)
                        {
                            string fn = "looping_" + SanitizeFilename(tag) + "_end.wav";
                            if (SaveClipAsWav(entry.EndClip, Path.Combine(dirFolder, fn)))
                            {
                                clips.Add(new AudioExportSfxClipDto
                                    { tag = tag, type = "looping_end",
                                        filename = "sfx/" + SanitizeFilename(a.id) + "/" + fn });
                            }
                        }
                    }
                }

                sfxList.Add(new AudioExportSfxDirDto
                    { id = a.id, nameZh = a.nameZh, clips = clips.ToArray() });
            }

            // Phase 5: Ambience tags (0.75 – 0.85)
            if (EditorUtility.DisplayCancelableProgressBar("Export Audio",
                "Resolving ambience tags...", 0.75f))
                return;

            var ambList = new List<AudioExportAmbienceDto>();
            foreach (var tagName in Enum.GetNames(
                         typeof(LevelInfoSO.GameLoopingAudioTag)))
            {
                if (tagName == "COUNT") continue;
                AudioExportAmbienceDto info;
                if (ambienceMap.TryGetValue(tagName, out info))
                    ambList.Add(info);
                else
                    ambList.Add(new AudioExportAmbienceDto
                        { tag = tagName, found = false });
            }

            // Phase 6: Manifest (0.85 – 0.95)
            if (EditorUtility.DisplayCancelableProgressBar("Export Audio",
                "Writing manifest...", 0.85f))
                return;

            var manifest = new AudioExportManifestDto
            {
                generatedAt = DateTime.UtcNow.ToString("o"),
                bgm = bgmList.ToArray(),
                sfx = sfxList.ToArray(),
                ambiences = ambList.ToArray()
            };
            File.WriteAllText(
                Path.Combine(exportRoot, "audio-exports.json"),
                JsonUtility.ToJson(manifest, true));

            // Phase 7: Cleanup
            EditorUtility.DisplayProgressBar("Export Audio", "Unloading AssetBundles...", 0.95f);
            if (!_bundlesReused)
            {
                foreach (var kv in bundles)
                    kv.Value.Unload(true);
            }

            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("Export Audio",
                "Export complete!\n\n" + exportRoot, "OK");
        }
        catch (Exception ex)
        {
            EditorUtility.ClearProgressBar();
            Debug.LogError("Export Audio failed: " + ex);
            EditorUtility.DisplayDialog("Export Audio Error", ex.Message, "OK");
        }
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

    private static bool SaveClipAsWav(AudioClip clip, string path)
    {
        if (clip.loadState != AudioDataLoadState.Loaded)
            clip.LoadAudioData();

        var wavBytes = EncodeWav(clip);
        if (wavBytes == null)
            return false;
        File.WriteAllBytes(path, wavBytes);
        return true;
    }

    private static byte[] EncodeWav(AudioClip clip)
    {
        int sampleCount = clip.samples * clip.channels;
        var floatData = new float[sampleCount];
        try
        {
            clip.GetData(floatData, 0);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("Export Audio: GetData failed for " + clip.name + " (" + clip.frequency + "Hz, " + clip.channels + "ch) — " + ex.Message);
            return null;
        }

        var intData = new short[floatData.Length];
        for (int i = 0; i < floatData.Length; i++)
            intData[i] = (short)Mathf.Clamp(floatData[i] * 32767f, -32768, 32767);

        var byteData = new byte[intData.Length * 2];
        Buffer.BlockCopy(intData, 0, byteData, 0, byteData.Length);

        int sampleRate = clip.frequency;
        int channels = clip.channels;
        int byteRate = sampleRate * channels * 2;
        int headerSize = 44;

        using (var ms = new MemoryStream(headerSize + byteData.Length))
        using (var bw = new BinaryWriter(ms))
        {
            bw.Write(new[] { 'R', 'I', 'F', 'F' });
            bw.Write(36 + byteData.Length);
            bw.Write(new[] { 'W', 'A', 'V', 'E' });
            bw.Write(new[] { 'f', 'm', 't', ' ' });
            bw.Write(16);
            bw.Write((short)1); // PCM
            bw.Write((short)channels);
            bw.Write(sampleRate);
            bw.Write(byteRate);
            bw.Write((short)(channels * 2)); // blockAlign
            bw.Write((short)16); // bitsPerSample
            bw.Write(new[] { 'd', 'a', 't', 'a' });
            bw.Write(byteData.Length);
            bw.Write(byteData);
            return ms.ToArray();
        }
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
