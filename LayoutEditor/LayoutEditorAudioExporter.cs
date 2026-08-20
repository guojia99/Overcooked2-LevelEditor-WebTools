using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using LevelEditorStub;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 音频导出：扫描 common01/common02 的 BGM 与 AudioDirectory PseudoPrefabSO，
/// 直接解析游戏 AssetBundle（UnityFS/LZ4/SerializedFile/TypeTree，见 AudioFs/），
/// 从 FSB5 提取音频：Vorbis → 无损重组 .ogg，IMA ADPCM → 解码 .wav，
/// 写出到仓库根目录 audio-exports/，并生成 audio-exports.json 供 web 编辑器试听。
/// 不依赖 python / libvorbis；不通过 AudioClip.GetData（压缩 clip 不支持）。
/// </summary>
public static class LayoutEditorAudioExporter
{
    private struct AudioAssetInfo
    {
        public string guid;
        public string id;
        public string bundleName;
        public string assetPath; // in-bundle container path
        public string nameZh;
    }

    // bundle 环境（含跨 bundle PPtr 解析）
    private sealed class Bundle
    {
        public string name;
        public LayoutEditor.AudioFs.AudioFsBundle raw;
        public Dictionary<string, LayoutEditor.AudioFs.AudioFsSerializedFile> files =
            new Dictionary<string, LayoutEditor.AudioFs.AudioFsSerializedFile>(StringComparer.OrdinalIgnoreCase);
        public byte[] data;
    }

    private sealed class ObjRef
    {
        public LayoutEditor.AudioFs.AudioFsObjectInfo obj;
        public LayoutEditor.AudioFs.AudioFsSerializedFile sf;
        public Bundle bundle;
        public LayoutEditor.AudioFs.AudioFsValue Read() { return obj.ReadValue(bundle.data); }
    }

    private static readonly Dictionary<string, Bundle> _bundles =
        new Dictionary<string, Bundle>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> _cabToBundle =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private static readonly List<string> _bundleOrder = new List<string>();
    private static string _bundlesDir;
    private static bool _tablesLoaded;

    private static bool _exporting;

    public static void ExportAudioForWeb()
    {
        if (_exporting)
        {
            EditorUtility.DisplayDialog("Export Audio", "上一次导出仍在进行中，请稍候。", "OK");
            return;
        }

        _exporting = true;
        var failures = new List<string>();

        try
        {
            // ---- Phase 1: tables ----
            EditorUtility.DisplayProgressBar("Export Audio", "加载解析表…", 0.01f);
            LoadTables();

            // ---- Phase 2: Scan ----
            EditorUtility.DisplayProgressBar("Export Audio", "扫描 BGM 资源…", 0.03f);
            var musicAssets = ScanAudioAssets("audio/music");
            EditorUtility.DisplayProgressBar("Export Audio", "扫描音效集资源…", 0.05f);
            var dirAssets = ScanAudioAssets("audio/AudioDirectories");

            // ---- Phase 3: Prepare output ----
            string exportRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../audio-exports"));
            if (Directory.Exists(exportRoot))
                Directory.Delete(exportRoot, true);
            Directory.CreateDirectory(Path.Combine(exportRoot, "bgm"));
            Directory.CreateDirectory(Path.Combine(exportRoot, "sfx"));

            _bundlesDir = Path.Combine(Application.streamingAssetsPath, "Windows").Replace('\\', '/');

            // ---- Phase 4: Export ----
            var bgmList = new List<AudioExportBgmDto>();
            var sfxList = new List<AudioExportSfxDirDto>();
            var ambienceCandidates = new Dictionary<string, string[]>(); // tag -> {filename, dirId}
            int okCount = 0;
            int failCount = 0;
            bool canceled = false;

            // BGM
            for (int i = 0; i < musicAssets.Count; i++)
            {
                var a = musicAssets[i];
                float p = 0.10f + 0.45f * ((float)i / Mathf.Max(1, musicAssets.Count));
                if (EditorUtility.DisplayCancelableProgressBar("Export Audio",
                    "导出 BGM " + (i + 1) + "/" + musicAssets.Count + ": " + a.nameZh, p))
                {
                    canceled = true;
                    break;
                }

                string rel = "bgm/" + SanitizeFilename(a.id);
                try
                {
                    string ext = ExportClipByContainer(a.bundleName, a.assetPath, exportRoot, rel);
                    bgmList.Add(new AudioExportBgmDto
                        { guid = a.guid, id = a.id, nameZh = a.nameZh, filename = rel + ext });
                    okCount++;
                }
                catch (Exception ex)
                {
                    failCount++;
                    failures.Add(rel + " — " + ex.Message);
                }
            }

            // SFX directories
            if (!canceled)
            {
                for (int i = 0; i < dirAssets.Count; i++)
                {
                    var a = dirAssets[i];
                    float p = 0.55f + 0.40f * ((float)i / Mathf.Max(1, dirAssets.Count));
                    if (EditorUtility.DisplayCancelableProgressBar("Export Audio",
                        "导出音效集 " + (i + 1) + "/" + dirAssets.Count + ": " + a.nameZh, p))
                    {
                        canceled = true;
                        break;
                    }

                    string dirFolder = "sfx/" + SanitizeFilename(a.id);
                    try
                    {
                        var clips = ExportDirectory(a.bundleName, a.assetPath, dirFolder, exportRoot,
                            ambienceCandidates, failures, ref okCount, ref failCount);
                        sfxList.Add(new AudioExportSfxDirDto { id = a.id, nameZh = a.nameZh, clips = clips });
                    }
                    catch (Exception ex)
                    {
                        failures.Add(dirFolder + " — " + ex.Message);
                        sfxList.Add(new AudioExportSfxDirDto
                            { id = a.id, nameZh = a.nameZh, clips = new AudioExportSfxClipDto[0] });
                    }
                    TrimBundleCache();
                }
            }

            // ---- Phase 5: Write manifest ----
            EditorUtility.DisplayProgressBar("Export Audio", "写出 manifest…", 0.96f);

            var ambList = new List<AudioExportAmbienceDto>();
            foreach (var tagName in Enum.GetNames(typeof(LevelInfoSO.GameLoopingAudioTag)))
            {
                if (tagName == "COUNT") continue;
                string[] cand;
                if (ambienceCandidates.TryGetValue(tagName, out cand))
                    ambList.Add(new AudioExportAmbienceDto
                        { tag = tagName, found = true, filename = cand[0], dirId = cand[1] });
                else
                    ambList.Add(new AudioExportAmbienceDto { tag = tagName, found = false });
            }

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

            // ---- Phase 6: Summary ----
            if (failures.Count > 0)
            {
                var sb = new StringBuilder();
                int max = Mathf.Min(failures.Count, 50);
                sb.AppendLine("Export Audio: 失败清单（前 " + max + "/" + failures.Count + " 个）：");
                for (int i = 0; i < max; i++)
                    sb.AppendLine("  " + failures[i]);
                Debug.LogWarning(sb.ToString());
            }

            string msg = string.Format("{0}完成！\n\n成功 {1} 个，失败 {2} 个。\n\n输出目录：\n{3}",
                canceled ? "导出已取消（已写出已完成部分）" : "导出", okCount, failCount, exportRoot);
            if (failures.Count > 0)
                msg += "\n\n⚠️ 有 " + failures.Count + " 项失败，详见 Console 日志。";
            EditorUtility.DisplayDialog("Export Audio", msg, "OK");
        }
        catch (Exception ex)
        {
            Debug.LogError("Export Audio failed: " + ex);
            EditorUtility.DisplayDialog("Export Audio Error", ex.Message, "OK");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            _bundles.Clear();
            _cabToBundle.Clear();
            _bundleOrder.Clear();
            _exporting = false;
        }
    }

    // -------------------------------------------------------------
    // 导出一个 AudioDirectory 的全部 clip
    // -------------------------------------------------------------

    private static AudioExportSfxClipDto[] ExportDirectory(string bundleName, string containerPath, string dirFolder,
        string exportRoot, Dictionary<string, string[]> ambienceCandidates,
        List<string> failures, ref int okCount, ref int failCount)
    {
        var dirObj = FindByContainer(bundleName, containerPath);
        if (dirObj == null)
            throw new Exception("AudioDirectoryData 未找到（bundle=" + bundleName + "，路径=" + containerPath + "）");

        var tree = dirObj.Read();
        string outDir = Path.Combine(exportRoot, dirFolder.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

        var clips = new List<AudioExportSfxClipDto>();
        var usedNames = new Dictionary<string, int>();

        var oneShot = tree.Field("OneShotAudio");
        if (oneShot != null)
        {
            var arr = AsArray(oneShot.value);
            for (int e = 0; e < arr.Count; e++)
                ExportEntry(dirObj, arr[e], "AudioFile", "oneshot", typeofGameOneShotTag,
                    dirFolder, exportRoot, usedNames, clips, ambienceCandidates, false,
                    failures, ref okCount, ref failCount);
        }

        var looping = tree.Field("LoopingAudio");
        if (looping != null)
        {
            var arr = AsArray(looping.value);
            for (int e = 0; e < arr.Count; e++)
            {
                ExportEntry(dirObj, arr[e], "AudioFile", "looping", typeofGameLoopingTag,
                    dirFolder, exportRoot, usedNames, clips, ambienceCandidates, true,
                    failures, ref okCount, ref failCount);
                ExportEntry(dirObj, arr[e], "StartClip", "looping_start", typeofGameLoopingTag,
                    dirFolder, exportRoot, usedNames, clips, ambienceCandidates, false,
                    failures, ref okCount, ref failCount);
                ExportEntry(dirObj, arr[e], "EndClip", "looping_end", typeofGameLoopingTag,
                    dirFolder, exportRoot, usedNames, clips, ambienceCandidates, false,
                    failures, ref okCount, ref failCount);
            }
        }

        return clips.ToArray();
    }

    private static Type typeofGameOneShotTag;
    private static Type typeofGameLoopingTag;

    private static void InitTagTypes()
    {
        try
        {
            var osEntry = typeof(AudioDirectoryData).GetNestedType("OneShotAudioDirectoryEntry",
                BindingFlags.Public | BindingFlags.NonPublic);
            if (osEntry != null)
            {
                var f = osEntry.GetField("Tag", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (f != null) typeofGameOneShotTag = f.FieldType;
            }
            var lpEntry = typeof(AudioDirectoryData).GetNestedType("LoopingAudioDirectoryEntry",
                BindingFlags.Public | BindingFlags.NonPublic);
            if (lpEntry != null)
            {
                var f = lpEntry.GetField("Tag", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (f != null) typeofGameLoopingTag = f.FieldType;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Export Audio: Tag 枚举反射失败（将以数值命名）：" + ex.Message);
        }
    }

    private static string TagToName(Type enumType, long value)
    {
        if (enumType != null && enumType.IsEnum)
        {
            try { return Enum.ToObject(enumType, value).ToString(); }
            catch (System.Exception) { }
        }
        return value.ToString();
    }

    private static void ExportEntry(ObjRef dirObj, LayoutEditor.AudioFs.AudioFsValue entry, string field,
        string typePrefix, Type tagEnumType, string dirFolder, string exportRoot,
        Dictionary<string, int> usedNames, List<AudioExportSfxClipDto> clips,
        Dictionary<string, string[]> ambienceCandidates, bool isAmbienceCandidate,
        List<string> failures, ref int okCount, ref int failCount)
    {
        var f = entry.Field(field);
        if (f == null) return;
        var pptr = f.value;
        long mpid = pptr.Field("m_PathID").value.AsInt();
        if (mpid == 0) return;
        int mfid = (int)pptr.Field("m_FileID").value.AsInt();

        string tag = TagToName(tagEnumType, entry.Field("Tag").value.AsInt());
        string baseName = typePrefix + "_" + SanitizeFilename(tag);
        string name = baseName;
        int used;
        if (usedNames.TryGetValue(baseName, out used))
            name = baseName + "_" + (used + 1);
        usedNames[baseName] = used + 1;

        string rel = dirFolder + "/" + name;
        try
        {
            var clip = ResolvePPtr(dirObj, mfid, mpid);
            if (clip == null) throw new Exception("clip 对象未解析（m_FileID=" + mfid + "）");
            string outPath = Path.Combine(exportRoot, rel.Replace('/', Path.DirectorySeparatorChar));
            string ext = ExtractClip(clip, outPath);
            clips.Add(new AudioExportSfxClipDto { tag = tag, type = typePrefix, filename = rel + ext });
            okCount++;
            if (isAmbienceCandidate && !ambienceCandidates.ContainsKey(tag))
                ambienceCandidates[tag] = new[] { rel + ext, Path.GetFileName(dirFolder) };
        }
        catch (Exception ex)
        {
            failCount++;
            failures.Add(rel + " — " + ex.Message);
        }
    }

    /// <summary>按 container 路径取 clip 并导出；返回扩展名（.ogg/.wav）。</summary>
    private static string ExportClipByContainer(string bundleName, string containerPath, string exportRoot, string relNoExt)
    {
        var clip = FindByContainer(bundleName, containerPath);
        if (clip == null)
            throw new Exception("AudioClip 未找到（bundle=" + bundleName + "，路径=" + containerPath + "）");
        string ext = ExtractClip(clip, Path.Combine(exportRoot, relNoExt.Replace('/', Path.DirectorySeparatorChar)));
        return ext;
    }

    /// <summary>读取 clip 的 FSB5 并写出文件，返回扩展名。</summary>
    private static string ExtractClip(ObjRef clip, string outPathNoExt)
    {
        var cv = clip.Read();
        byte[] fsb = ReadClipResource(clip, cv);
        if (fsb.Length < 4 || fsb[0] != (byte)'F' || fsb[1] != (byte)'S' || fsb[2] != (byte)'B' || fsb[3] != (byte)'5')
            throw new Exception("非 FSB5 音频资源");
        int mode = LayoutEditor.AudioFs.AudioFsFsb5.Mode(fsb);
        var samples = LayoutEditor.AudioFs.AudioFsFsb5.Parse(fsb);
        if (samples.Count == 0) throw new Exception("FSB5 无 sample");
        var s = samples[0];

        if (mode == LayoutEditor.AudioFs.AudioFsFsb5.ModeVorbis)
        {
            var setup = LayoutEditor.AudioFs.AudioFsTables.GetVorbisSetup(s.vorbisCrc32);
            if (setup == null) throw new Exception("Vorbis 配置缺失（crc32=" + s.vorbisCrc32 + "）");
            byte[] ogg = LayoutEditor.AudioFs.AudioFsFsb5.RebuildVorbisOgg(s, setup, 1);
            File.WriteAllBytes(outPathNoExt + ".ogg", ogg);
            return ".ogg";
        }
        if (mode == LayoutEditor.AudioFs.AudioFsFsb5.ModeImaAdpcm)
        {
            short[] pcm = LayoutEditor.AudioFs.AudioFsFsb5.DecodeImaAdpcm(s);
            byte[] wav = LayoutEditor.AudioFs.AudioFsFsb5.BuildWav(pcm, s.channels, s.frequency);
            File.WriteAllBytes(outPathNoExt + ".wav", wav);
            return ".wav";
        }
        throw new Exception("不支持的音频格式 mode=" + mode);
    }

    private static List<LayoutEditor.AudioFs.AudioFsValue> AsArray(LayoutEditor.AudioFs.AudioFsValue v)
    {
        if (v.isObject) throw new Exception("expected array");
        return (List<LayoutEditor.AudioFs.AudioFsValue>)v.primitive;
    }

    // -------------------------------------------------------------
    // Bundle 环境：加载 / container 查找 / PPtr 解析 / 资源读取
    // -------------------------------------------------------------

    private static void LoadTables()
    {
        if (_tablesLoaded) return;
        string dir = Path.Combine(Application.dataPath, "Editor/LayoutEditor/AudioFs").Replace('\\', '/');
        LayoutEditor.AudioFs.AudioFsTables.LoadCommonStrings(Path.Combine(dir, "oc2-common-strings.txt"));
        LayoutEditor.AudioFs.AudioFsTables.LoadVorbisSetups(Path.Combine(dir, "vorbis-setup-tables.txt"));
        InitTagTypes();
        _tablesLoaded = true;
    }

    private static Bundle LoadBundle(string name)
    {
        Bundle b;
        if (_bundles.TryGetValue(name, out b))
        {
            if (b.data == null) b.data = b.raw.Data; // 被裁剪后重载
            return b;
        }
        string path = Path.Combine(_bundlesDir, name);
        b = new Bundle();
        b.name = name;
        b.raw = LayoutEditor.AudioFs.AudioFsBundle.Scan(path);
        b.data = b.raw.Data;
        foreach (var e in b.raw.entries)
        {
            _cabToBundle[e.name] = name;
            if (e.name.EndsWith(".resS", StringComparison.OrdinalIgnoreCase) ||
                e.name.EndsWith(".resource", StringComparison.OrdinalIgnoreCase))
                continue;
            var bytes = new byte[e.size];
            Buffer.BlockCopy(b.data, (int)e.offset, bytes, 0, (int)e.size);
            b.files[e.name] = LayoutEditor.AudioFs.AudioFsSerializedFile.Parse(e.name, bytes);
        }
        _bundles[name] = b;
        _bundleOrder.Add(name);
        return b;
    }

    /// <summary>限制缓存：保留最近 8 个 bundle 的解压数据（条目/序列化文件元数据保留）。</summary>
    private static void TrimBundleCache()
    {
        while (_bundleOrder.Count > 8)
        {
            string evict = _bundleOrder[0];
            _bundleOrder.RemoveAt(0);
            Bundle b;
            if (_bundles.TryGetValue(evict, out b))
                b.data = null;
        }
    }

    private static ObjRef FindByContainer(string bundleName, string containerPath)
    {
        var b = LoadBundle(bundleName);
        string target = (containerPath ?? "").ToLowerInvariant().Replace('\\', '/');
        foreach (var sf in b.files.Values)
        {
            var abObj = sf.FindByClass(142);
            if (abObj == null) continue;
            LayoutEditor.AudioFs.AudioFsValue abTree;
            try { abTree = abObj.ReadValue(b.data); }
            catch (Exception) { continue; }
            var cont = abTree.Field("m_Container");
            if (cont == null) continue;
            foreach (var pair in AsArray(cont.value))
            {
                var pf = pair.Fields;
                string path = pf[0].value.AsString();
                if (string.Compare((path ?? "").Replace('\\', '/'), target, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    var pptr = pf[1].value.Field("asset").value;
                    int mfid = (int)pptr.Field("m_FileID").value.AsInt();
                    long mpid = pptr.Field("m_PathID").value.AsInt();
                    return ResolvePPtr(new ObjRef { obj = abObj, sf = sf, bundle = b }, mfid, mpid);
                }
            }
        }
        return null;
    }

    private static string CabOfExternal(string ext)
    {
        // 形如 archive:/CAB-xxx/CAB-xxx（单斜杠）
        string cab = ext;
        int colon = cab.IndexOf(':');
        if (colon >= 0) cab = cab.Substring(colon + 1);
        cab = cab.TrimStart('/');
        int slash = cab.IndexOf('/');
        if (slash >= 0) cab = cab.Substring(0, slash);
        return cab;
    }

    private static ObjRef ResolvePPtr(ObjRef from, int mFileId, long mPathId)
    {
        if (mFileId == 0)
        {
            var o = from.sf.GetByPathId(mPathId);
            if (o != null) return new ObjRef { obj = o, sf = from.sf, bundle = from.bundle };
            foreach (var sf in from.bundle.files.Values)
            {
                var o2 = sf.GetByPathId(mPathId);
                if (o2 != null) return new ObjRef { obj = o2, sf = sf, bundle = from.bundle };
            }
            return null;
        }
        int idx = mFileId - 1;
        if (idx < 0 || idx >= from.sf.externals.Count) return null;
        string cab = CabOfExternal(from.sf.externals[idx]);
        string bundleName = FindBundleForCab(cab);
        if (bundleName == null) return null;
        var dep = LoadBundle(bundleName);
        foreach (var depSf in dep.files.Values)
        {
            var o = depSf.GetByPathId(mPathId);
            if (o != null) return new ObjRef { obj = o, sf = depSf, bundle = dep };
        }
        return null;
    }

    private static string FindBundleForCab(string cab)
    {
        if (_cabToBundle.ContainsKey(cab)) return _cabToBundle[cab];
        foreach (var f in Directory.GetFiles(_bundlesDir))
        {
            string n = Path.GetFileName(f);
            if (n.StartsWith(".") || n.EndsWith(".meta")) continue;
            try
            {
                var scanned = LayoutEditor.AudioFs.AudioFsBundle.Scan(f);
                foreach (var e in scanned.entries)
                    _cabToBundle[e.name] = n;
            }
            catch (Exception) { }
            if (_cabToBundle.ContainsKey(cab)) return _cabToBundle[cab];
        }
        return null;
    }

    private static byte[] ReadClipResource(ObjRef clip, LayoutEditor.AudioFs.AudioFsValue cv)
    {
        var b = clip.bundle;
        if (b.data == null) b.data = b.raw.Data;
        var res = cv.Field("m_Resource").value;
        string src = res.Field("m_Source").value.AsString();
        long off = res.Field("m_Offset").value.AsInt();
        long size = res.Field("m_Size").value.AsInt();
        string entryName = src;
        int slash = entryName.LastIndexOf('/');
        if (slash >= 0) entryName = entryName.Substring(slash + 1);
        var entry = b.raw.FindEntry(entryName);
        if (entry == null) throw new Exception("资源条目未找到：" + entryName);
        var buf = new byte[size];
        Buffer.BlockCopy(b.data, (int)(entry.offset + off), buf, 0, (int)size);
        return buf;
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
