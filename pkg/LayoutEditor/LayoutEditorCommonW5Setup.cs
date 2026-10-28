using System;
using System.Collections.Generic;
using System.IO;
using LevelEditorStub;
using UnityEditor;
using UnityEngine;

/// <summary>
/// commonW5 增量库（OC1/AYCE BGM · 桌台皮肤 · 自定义食材 · 卷饼菜谱）的 Unity 侧校验与补全。
///
/// 生成脚本 gen-commonw5.mjs 手写 YAML 时无法可靠序列化 UnityEngine.Object 直引、
/// Sprite 子资产与 SubTexture2D；本类在域重载后（幂等）补全：
///   1. PseudoPrefabSO.asset ← 本地 AudioClip / prefab（音乐、食材、桌台皮肤）；
///   2. IngredientOrderNode：m_iconSprite / m_crateLid 缺失时按图标补全，并校验 uID 唯一；
///   3. 缺 OGG / prefab 时仅告警，不阻断。
/// 也可手动执行菜单「Layout Editor/commonW5 校验并补全直引」。
/// </summary>
[InitializeOnLoad]
public static class LayoutEditorCommonW5Setup
{
    private const string Root = "Assets/commonW5";
    private const string PseudoRoot = Root + "/pseudo_prefab_so";
    private const string AudioRoot = Root + "/audio";
    private const string IngredientRoot = Root + "/ingredients";
    private const string CounterRoot = Root + "/counters";

    static LayoutEditorCommonW5Setup()
    {
        EditorApplication.delayCall += AutoVerify;
    }

    private static void AutoVerify()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
            return;
        if (!AssetDatabase.IsValidFolder(Root))
            return;
        VerifyAndRepair(false);
    }

    [MenuItem("Layout Editor/commonW5 校验并补全直引 (Verify commonW5)", false, 30)]
    public static void VerifyMenu()
    {
        VerifyAndRepair(true);
    }

    internal static void VerifyAndRepair(bool verbose)
    {
        int fixedCount = 0;
        int warnCount = 0;

        // ---- 1) PseudoPrefabSO 直引 ----
        var audioByClipName = CollectAssetPaths("t:AudioClip", AudioRoot);
        var prefabByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        AddPrefabPaths(prefabByName, IngredientRoot);
        AddPrefabPaths(prefabByName, CounterRoot);

        var soGuids = AssetDatabase.FindAssets("t:PseudoPrefabSO", new[] { PseudoRoot });
        for (int i = 0; i < soGuids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(soGuids[i]);
            var so = AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(path);
            if (so == null || so.asset != null)
                continue;
            // bundle 型 SO（如 TeacupSinkSO / TeacupReturnSO）由运行时按
            // bundleName+assetPath 加载，本地没有直引 prefab 是正常的。
            if (!string.IsNullOrEmpty(so.bundleName) && !string.IsNullOrEmpty(so.assetPath))
                continue;

            var id = Path.GetFileNameWithoutExtension(path);
            UnityEngine.Object target = null;

            if (path.Replace("\\", "/").IndexOf("/pseudo_prefab_so/audio/", StringComparison.Ordinal) >= 0)
            {
                string clipPath;
                if (audioByClipName.TryGetValue(id, out clipPath))
                    target = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
                if (target == null)
                {
                    Debug.LogWarning("[commonW5] 音乐 SO 找不到对应 OGG: " + path + "（期望同名 " + id + ".ogg）");
                    warnCount++;
                    continue;
                }
            }
            else
            {
                string prefabPath;
                if (!string.IsNullOrEmpty(so.prefabName) && prefabByName.TryGetValue(so.prefabName, out prefabPath))
                    target = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (target == null)
                {
                    Debug.LogWarning("[commonW5] SO 找不到直引 prefab: " + path
                        + "（prefabName=" + so.prefabName + "）");
                    warnCount++;
                    continue;
                }
            }

            so.asset = target;
            EditorUtility.SetDirty(so);
            fixedCount++;
            if (verbose)
                Debug.Log("[commonW5] 补全直引 " + path + " → " + AssetDatabase.GetAssetPath(target));
        }

        // ---- 2) IngredientOrderNode：图标 / crateLid / uID ----
        var nodeGuids = AssetDatabase.FindAssets("t:IngredientOrderNode", new[] { IngredientRoot });
        var uidSeen = new Dictionary<int, string>();
        for (int i = 0; i < nodeGuids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(nodeGuids[i]);
            var node = AssetDatabase.LoadAssetAtPath<IngredientOrderNode>(path);
            if (node == null)
                continue;

            if (node.m_uID <= 0)
            {
                Debug.LogWarning("[commonW5] 食材节点 uID 非法: " + path);
                warnCount++;
            }
            else
            {
                string other;
                if (uidSeen.TryGetValue(node.m_uID, out other))
                {
                    Debug.LogWarning("[commonW5] 食材节点 uID 冲突 " + node.m_uID + ": " + path + " ↔ " + other);
                    warnCount++;
                }
                else
                {
                    uidSeen[node.m_uID] = path;
                }
            }

            var sprite = node.m_iconSprite;
            if (sprite == null)
            {
                Debug.LogWarning("[commonW5] 食材节点缺少图标 sprite: " + path);
                warnCount++;
                continue;
            }

            bool crateMissing = node.m_crateLid == null
                || node.m_crateLid.m_atlasTexture == null
                || node.m_crateLid.m_subRect.width < 1f
                || node.m_crateLid.m_subRect.height < 1f;
            if (crateMissing)
            {
                node.m_crateLid = new SubTexture2D
                {
                    m_atlasTexture = sprite.texture,
                    m_subRect = sprite.textureRect
                };
                EditorUtility.SetDirty(node);
                fixedCount++;
                if (verbose)
                    Debug.Log("[commonW5] 补全 crateLid: " + path);
            }
        }

        if (fixedCount > 0)
            AssetDatabase.SaveAssets();

        if (verbose || fixedCount > 0 || warnCount > 0)
        {
            Debug.Log("[commonW5] 校验完成：补全 " + fixedCount + " 项，告警 " + warnCount + " 项"
                + "（音乐 " + audioByClipName.Count + " 个 OGG / prefab " + prefabByName.Count + " 个 / 食材节点 " + nodeGuids.Length + " 个）");
        }
    }

    private static Dictionary<string, string> CollectAssetPaths(string filter, string folder)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!AssetDatabase.IsValidFolder(folder))
            return map;
        var guids = AssetDatabase.FindAssets(filter, new[] { folder });
        for (int i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var name = Path.GetFileNameWithoutExtension(path);
            if (!map.ContainsKey(name))
                map[name] = path;
        }
        return map;
    }

    private static void AddPrefabPaths(Dictionary<string, string> map, string folder)
    {
        var paths = CollectAssetPaths("t:Prefab", folder);
        foreach (var kv in paths)
            map[kv.Key] = kv.Value;
    }
}

/// <summary>commonW5 首次导入完成后触发一次直引补全（域重载前 Import 尚未完成时兜底）。</summary>
public class LayoutEditorCommonW5SetupPostprocessor : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        if (importedAssets == null)
            return;
        for (int i = 0; i < importedAssets.Length; i++)
        {
            var p = importedAssets[i];
            if (!string.IsNullOrEmpty(p) &&
                p.Replace("\\", "/").StartsWith("Assets/commonW5/", StringComparison.Ordinal))
            {
                EditorApplication.delayCall += delegate { LayoutEditorCommonW5Setup.VerifyAndRepair(false); };
                return;
            }
        }
    }
}
