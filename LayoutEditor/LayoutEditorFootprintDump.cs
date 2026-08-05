using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-shot batch tool: measures the real XZ footprint of every art decor
/// prefab (common01/common02 prefabs/art/**) and writes the result to
/// layout-editor/scripts/data/measured-footprints.json, which
/// build-catalog.mjs merges into catalog.json so the web palette shows decor
/// at its true size (e.g. roads 1x2) instead of the 1x1 fallback.
/// Re-run this menu after art prefabs change, then rebuild the catalog.
/// </summary>
public static class LayoutEditorFootprintDump
{
    private const string OutputPath = "layout-editor/scripts/data/measured-footprints.json";
    private static readonly string[] ArtRoots = { "Assets/common01/prefabs/art", "Assets/common02/prefabs/art" };

    [Serializable]
    private class Entry
    {
        public string id;
        public string guid;
        public int cellsX;
        public int cellsZ;
    }

    [Serializable]
    private class Payload
    {
        public string generatedAt;
        public List<Entry> items;
    }

    [MenuItem("Tools/Layout Editor/导出装饰实测尺寸 (measured-footprints.json)", false, 211)]
    public static void Dump()
    {
        var entries = new List<Entry>();
        int failed = 0;

        foreach (var root in ArtRoots)
        {
            if (!AssetDatabase.IsValidFolder(root))
                continue;

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { root }))
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (prefab == null)
                    continue;

                var id = Path.GetFileNameWithoutExtension(assetPath);
                GameObject temp = null;
                try
                {
                    temp = UnityEngine.Object.Instantiate(prefab);
                    temp.hideFlags = HideFlags.HideAndDontSave;

                    // PseudoPrefab placeholders have no meshes of their own —
                    // spawn the real bundle appearance first (edit-time path).
                    var pseudo = temp.GetComponent<LevelEditor.PseudoPrefab>();
                    if (pseudo != null)
                        pseudo.ResetChild();

                    var fp = LayoutEditorFootprintMeasure.MeasureCells(temp);
                    bool measurable = temp.GetComponentsInChildren<MeshRenderer>(false).Length > 0;
                    if (!measurable)
                    {
                        failed++;
                        Debug.LogWarning("[FootprintDump] no renderers, skipped: " + assetPath);
                        continue;
                    }

                    entries.Add(new Entry { id = id, guid = guid, cellsX = fp.cellsX, cellsZ = fp.cellsZ });
                }
                catch (Exception e)
                {
                    failed++;
                    Debug.LogWarning("[FootprintDump] failed for " + assetPath + ": " + e.Message);
                }
                finally
                {
                    if (temp != null)
                        UnityEngine.Object.DestroyImmediate(temp);
                }
            }
        }

        entries.Sort((a, b) => string.CompareOrdinal(a.id, b.id));

        var payload = new Payload
        {
            generatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            items = entries
        };
        var abs = Path.GetFullPath(OutputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(abs));
        File.WriteAllText(abs, JsonUtility.ToJson(payload, true));
        Debug.Log("[FootprintDump] wrote " + entries.Count + " entries to " + abs
            + (failed > 0 ? " (" + failed + " skipped)" : ""));
    }
}
