using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// Chinese / English display names. Lookup order: layout-editor/scripts/data/names-dictionary.json
/// (shared with build-catalog.mjs) -> 使用手册.md tables -> id fallback.
/// </summary>
public static class LayoutEditorManualLookup
{
    private struct NameRow
    {
        public string Zh;
        public string En;
    }

#pragma warning disable 0649 // fields assigned by JsonUtility deserialization
    [Serializable]
    private class DictionaryDoc
    {
        public int schemaVersion;
        public DictionaryName[] names;
    }

    [Serializable]
    private class DictionaryName
    {
        public string id;
        public string zh;
        public string en;
    }
#pragma warning restore 0649

    private static Dictionary<string, NameRow> _byId;
    private static bool _loaded;
    private static bool _dictionaryLoaded;

    /// <summary>False when names-dictionary.json is missing/unreadable (bridge likely outdated).</summary>
    public static bool DictionaryLoaded
    {
        get
        {
            EnsureLoaded();
            return _dictionaryLoaded;
        }
    }

    /// <summary>Reload dictionary + manual (e.g. after editing the JSON files).</summary>
    public static void InvalidateCache()
    {
        _loaded = false;
    }

    public static bool TryGet(string id, out string nameZh, out string nameEn)
    {
        EnsureLoaded();
        NameRow row;
        if (!string.IsNullOrEmpty(id) && _byId.TryGetValue(id, out row))
        {
            nameZh = row.Zh;
            nameEn = row.En;
            return true;
        }

        nameZh = FallbackZh(id);
        nameEn = FallbackEn(id);
        return false;
    }

    public static bool TryGetLevelSetName(string setName, string id, out string nameZh, out string nameEn)
    {
        if (!string.IsNullOrEmpty(setName) && !string.IsNullOrEmpty(id))
        {
            var namesPath = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(Application.dataPath, "../Assets/LevelSets/" + setName + "/custom_recipes/names.json"));
            if (System.IO.File.Exists(namesPath))
            {
                try
                {
                    var text = System.IO.File.ReadAllText(namesPath);
                    var doc = JsonUtility.FromJson<DictionaryDoc>(text);
                    if (doc != null && doc.names != null)
                    {
                        foreach (var n in doc.names)
                        {
                            if (n != null && n.id == id && !string.IsNullOrEmpty(n.zh))
                            {
                                nameZh = n.zh;
                                nameEn = string.IsNullOrEmpty(n.en) ? id : n.en;
                                return true;
                            }
                        }
                    }
                }
                catch { }
            }
        }
        return TryGet(id, out nameZh, out nameEn);
    }

    private static void EnsureLoaded()
    {
        if (_loaded)
            return;
        _loaded = true;
        _byId = new Dictionary<string, NameRow>(StringComparer.Ordinal);

        LoadDictionaryJson();
        LoadManual();
    }

    private static void LoadDictionaryJson()
    {
        _dictionaryLoaded = false;
        var dictPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../layout-editor/scripts/data/names-dictionary.json"));
        if (!File.Exists(dictPath))
            return;

        string text;
        try
        {
            text = File.ReadAllText(dictPath);
        }
        catch
        {
            return;
        }

        DictionaryDoc doc;
        try
        {
            doc = JsonUtility.FromJson<DictionaryDoc>(text);
        }
        catch
        {
            return;
        }
        if (doc == null || doc.names == null)
            return;

        foreach (var n in doc.names)
        {
            if (n == null || string.IsNullOrEmpty(n.id) || string.IsNullOrEmpty(n.zh))
                continue;
            _byId[n.id] = new NameRow { Zh = n.zh, En = string.IsNullOrEmpty(n.en) ? n.id : n.en };
        }
        _dictionaryLoaded = true;
    }

    private static void LoadManual()
    {
        var manualPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../\u4F7F\u7528\u624B\u518C.md"));
        if (!File.Exists(manualPath))
            return;

        string text;
        try
        {
            text = File.ReadAllText(manualPath);
        }
        catch
        {
            return;
        }

        foreach (var line in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!line.StartsWith("|", StringComparison.Ordinal))
                continue;
            if (line.IndexOf("---|", StringComparison.Ordinal) >= 0)
                continue;

            var parts = line.Split('|');
            if (parts.Length < 4)
                continue;

            var id = parts[1].Trim();
            var zh = parts[2].Trim();
            var en = parts.Length >= 4 ? parts[3].Trim() : id;

            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(zh))
                continue;
            if (id == "ID" || id.Contains("文件位置"))
                continue;
            if (!Regex.IsMatch(id, @"^[A-Za-z0-9_][A-Za-z0-9_.\- ]*$"))
                continue;
            if (zh.StartsWith("`", StringComparison.Ordinal))
                continue;

            // names-dictionary.json wins over the manual table.
            if (!_byId.ContainsKey(id))
                _byId[id] = new NameRow { Zh = zh, En = string.IsNullOrEmpty(en) ? id : en };
        }
    }

    private static string FallbackZh(string id)
    {
        if (string.IsNullOrEmpty(id))
            return "—";
        if (id.EndsWith("SO", StringComparison.Ordinal))
            return id.Substring(0, id.Length - 2);
        return id;
    }

    private static string FallbackEn(string id)
    {
        if (string.IsNullOrEmpty(id))
            return "—";
        var baseId = id;
        if (baseId.EndsWith("SO", StringComparison.Ordinal))
            baseId = baseId.Substring(0, baseId.Length - 2);
        return Regex.Replace(baseId, "([a-z])([A-Z])", "$1 $2");
    }
}
