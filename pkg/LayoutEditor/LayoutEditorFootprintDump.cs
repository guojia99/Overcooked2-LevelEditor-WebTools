using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-shot batch tool: measures the real XZ footprint of every art decor
/// prefab (common01/common02/prefabs/art/** and common03/prefabs/**/art/**) and
/// writes the result to layout-editor/scripts/data/measured-footprints.json,
/// which build-catalog.mjs merges into catalog.json so the web palette shows
/// decor at its true size (e.g. roads 1x2) instead of the 1x1 fallback.
/// Re-run this menu after art prefabs change (incl. common03 decor), then
/// rebuild the catalog.
/// </summary>
public static class LayoutEditorFootprintDump
{
    private const string OutputPath = "layout-editor/scripts/data/measured-footprints.json";
    private static readonly string[] ArtRoots = {
        "Assets/common01/prefabs/art",
        "Assets/common02/prefabs/art",
        "Assets/common03/prefabs",
        "Assets/commonW1/prefabs",
    };

    /// <summary>3D 视图专用的全量竖向包围盒（与占格表刻意分文件，见 DumpBounds）。</summary>
    private const string BoundsOutputPath = "layout-editor/scripts/data/measured-bounds.json";
    private static readonly string[] AllPrefabRoots = {
        "Assets/common01/prefabs",
        "Assets/common02/prefabs",
        "Assets/common03/prefabs",
        "Assets/commonW1/prefabs",
        "Assets/commonW2/prefabs",
    };

    [Serializable]
    private class Entry
    {
        public string id;
        public string guid;
        public float cellsX;
        public float cellsZ;
        public float sizeY;
    }

    [Serializable]
    private class Payload
    {
        public string generatedAt;
        public List<Entry> items;
    }

    [Serializable]
    private class BoundsEntry
    {
        public string id;
        public string guid;
        public float sizeY;
        public float minY;
    }

    [Serializable]
    private class BoundsPayload
    {
        public string generatedAt;
        public List<BoundsEntry> items;
    }

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
                // common03 / commonW1 根覆盖全部分类，只测装饰物（占位 prefab 走 PseudoPrefab
                // 家族 ResetChild 从 bundle 生成真实网格后测量，与 common01/02 一致）。
                if (!assetPath.Contains("/art/") && !assetPath.Contains("/backgrounds/"))
                    continue;
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
                    float sizeY = LayoutEditorFootprintMeasure.MeasureHeight(temp);
                    bool measurable = temp.GetComponentsInChildren<MeshRenderer>(false).Length > 0;
                    if (!measurable)
                    {
                        failed++;
                        Debug.LogWarning("[FootprintDump] no renderers, skipped: " + assetPath);
                        continue;
                    }

                    entries.Add(new Entry { id = id, guid = guid, cellsX = fp.cellsX, cellsZ = fp.cellsZ, sizeY = sizeY });
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

    /// <summary>
    /// Web 3D 视图专用：测量【全部】prefab（不只装饰）的竖向包围盒，输出
    /// {id, guid, sizeY, minY} 到 measured-bounds.json。
    ///
    /// 为什么另开一个文件而不是并进 measured-footprints.json：后者的 cellsX/cellsZ
    /// 被 build-catalog.mjs 的 isDecorCategory 闸门挡着只给装饰用，一旦把柜台/厨具
    /// 的实测占格混进去，会覆盖手工校准的 FOOTPRINT_OVERRIDES，属高危回归。分文件
    /// 后占格链路零改动，3D 只消费 sizeY/minY。
    ///
    /// minY 是盒底相对 pivot 的偏移：柜台 pivot 在底（≈0），汤锅 pivot 在中（负值）。
    /// 3D 从 localPosition.y + minY 起画高 sizeY 的盒子，否则中心 pivot 的物件会悬空。
    /// </summary>
    public static void DumpBounds()
    {
        var entries = new List<BoundsEntry>();
        var seen = new HashSet<string>();
        int failed = 0;

        try
        {
            foreach (var root in AllPrefabRoots)
            {
                if (!AssetDatabase.IsValidFolder(root))
                    continue;

                var guids = AssetDatabase.FindAssets("t:Prefab", new[] { root });
                for (int i = 0; i < guids.Length; i++)
                {
                    var guid = guids[i];
                    var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    var id = Path.GetFileNameWithoutExtension(assetPath);
                    // 同名 prefab 跨库重复时以先扫到的为准（与 catalog 的 id 唯一性一致）。
                    if (string.IsNullOrEmpty(id) || !seen.Add(id))
                        continue;

                    if (EditorUtility.DisplayCancelableProgressBar(
                            "导出 3D 高度数据",
                            root + "  (" + (i + 1) + "/" + guids.Length + ")  " + id,
                            guids.Length > 0 ? (float)i / guids.Length : 0f))
                    {
                        Debug.LogWarning("[BoundsDump] cancelled by user");
                        return;
                    }

                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                    if (prefab == null)
                        continue;

                    GameObject temp = null;
                    try
                    {
                        temp = UnityEngine.Object.Instantiate(prefab);
                        temp.hideFlags = HideFlags.HideAndDontSave;

                        // PseudoPrefab 占位体本身没有网格，先按编辑期路径生成真实外观。
                        // 必须递归处理：dlc08_cannon / Pushable_Object 这类复合体自身
                        // 没有 pseudoPrefabSO，真实网格挂在子层的 PseudoPrefab 上，
                        // 只对根调用会量不到任何东西。
                        SpawnPseudoChildrenRecursive(temp);

                        float sizeY;
                        float minY;
                        if (!LayoutEditorFootprintMeasure.MeasureBounds(temp, out sizeY, out minY))
                            continue;
                        if (sizeY <= 0.0001f)
                            continue;

                        entries.Add(new BoundsEntry
                        {
                            id = id,
                            guid = guid,
                            sizeY = sizeY,
                            minY = minY
                        });
                    }
                    catch (Exception e)
                    {
                        failed++;
                        Debug.LogWarning("[BoundsDump] failed for " + assetPath + ": " + e.Message);
                    }
                    finally
                    {
                        if (temp != null)
                            UnityEngine.Object.DestroyImmediate(temp);
                    }
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        entries.Sort((a, b) => string.CompareOrdinal(a.id, b.id));

        var payload = new BoundsPayload
        {
            generatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            items = entries
        };
        var abs = Path.GetFullPath(BoundsOutputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(abs));
        File.WriteAllText(abs, JsonUtility.ToJson(payload, true));
        Debug.Log("[BoundsDump] wrote " + entries.Count + " entries to " + abs
            + (failed > 0 ? " (" + failed + " skipped)" : ""));
    }

    /// <summary>
    /// 递归生成真实外观：先处理根，再处理生成出来的子层里新出现的 PseudoPrefab。
    /// 复合体（大炮、可推动火锅等）自身没有 pseudoPrefabSO，网格全在子层；
    /// 而子层 ResetChild 后又可能带出下一级占位体，所以要迭代到收敛。
    /// 设了轮次上限，避免异常数据造成死循环。
    /// </summary>
    private static void SpawnPseudoChildrenRecursive(GameObject root)
    {
        if (root == null)
            return;

        var done = new HashSet<int>();
        for (int round = 0; round < 4; round++)
        {
            var pseudos = root.GetComponentsInChildren<LevelEditor.PseudoPrefab>(true);
            if (pseudos == null || pseudos.Length == 0)
                return;

            bool didWork = false;
            for (int i = 0; i < pseudos.Length; i++)
            {
                var p = pseudos[i];
                if (p == null)
                    continue;
                int id = p.GetInstanceID();
                if (done.Contains(id))
                    continue;
                done.Add(id);
                try
                {
                    p.ResetChild();
                    didWork = true;
                }
                catch (Exception e)
                {
                    // 单个占位体失败不影响整体测量（缺它一块总比整条目丢失强）。
                    Debug.LogWarning("[BoundsDump] ResetChild failed on " + p.name + ": " + e.Message);
                }
            }
            if (!didWork)
                return;
        }
    }
}
