using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>Chinese / English display names from 使用手册.md tables (same IDs as build-catalog.mjs).</summary>
public static class LayoutEditorManualLookup
{
    private struct NameRow
    {
        public string Zh;
        public string En;
    }

    private static Dictionary<string, NameRow> _byId;
    private static bool _loaded;

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

    private static void EnsureLoaded()
    {
        if (_loaded)
            return;
        _loaded = true;
        _byId = new Dictionary<string, NameRow>(StringComparer.Ordinal);

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
