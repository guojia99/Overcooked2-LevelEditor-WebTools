using System.Collections.Generic;
using LevelEditor;
using LevelEditorStub;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 火锅大锅「默认能放/有汤面」：把所选火锅菜谱的叶食材与 permutations 生/熟中间产物
/// 自动登记为大锅（utensil_large_pot_01 / utensil_dlc10_large_pot_01）stub 的
/// allowedIngredientSOs。
///
/// 背景：PseudoPrefabCookingUtensil.Setup() 会把 ContentsCosmeticDecisions.m_prefabLookup
/// 置空，只有 allowedIngredientSOs 非空时才会重建 CookableContainer.m_approvedContentsList
/// （内容物/汤面显示、放入许可都靠它）。烤盘有 LayoutEditorRoastTrayFill 自动登记，
/// 火锅大锅此前没有对应 Fill——手动放置的大锅丢入面条后无汤面、灶台不加热。
///
/// 处理（与烤盘 Fill 同模式，保存菜谱/写回场景后，只增不删、幂等）：
///  - 叶食材：noodles/meat/prawn/bokchoy（按所选菜谱 DLC，common03/Ingredients 解析）。
///  - 中间产物：hotpot_permutationsraw / hotpot_permutationscooked（PseudoPrefabSORecipe，
///    与菜谱同 DLC），重建 lookup 后多食材/熟内容才能显示汤面模型。
/// 宿主文件一律不动。
/// </summary>
public static class LayoutEditorHotPotFill
{
    /// <summary>把所选火锅菜谱的叶食材 + permutations 生/熟节点合并进场景所有大锅 stub。</summary>
    public static void EnsureHotPotIngredients(LevelInfoSO info)
    {
        if (info == null)
            return;
        var extra = CollectHotPotNodes(info);
        if (extra.Count == 0)
            return;
        var extraGuids = new List<string>(extra.Count);
        foreach (var so in extra)
        {
            var p = AssetDatabase.GetAssetPath(so);
            if (!string.IsNullOrEmpty(p))
                extraGuids.Add(AssetDatabase.AssetPathToGUID(p));
        }
        if (extraGuids.Count == 0)
            return;

        bool sceneDirty = false;
        foreach (var stub in Object.FindObjectsOfType<PseudoPrefabStub>())
        {
            if (stub == null || stub.gameObject == null)
                continue;
            // 清理：可移动火锅此前被误挂的锅具 stub（Setup NRE 源），直接移除。
            if (IsPushablePot(stub) || IsTrack(stub))
            {
                var badDerived = stub.GetComponent<PseudoPrefabCookingUtensilStub>();
                var badRuntime = stub.GetComponent<LevelEditor.PseudoPrefabCookingUtensil>();
                if (badDerived != null || badRuntime != null)
                {
                    if (badDerived != null) Undo.DestroyObjectImmediate(badDerived);
                    if (badRuntime != null) Undo.DestroyObjectImmediate(badRuntime);
                    sceneDirty = true;
                    LayoutEditorLog.Log("[HotPotFill] 已移除可移动火锅上误挂的锅具 stub: " + stub.gameObject.name);
                }
                continue;
            }
            if (!IsLargePot(stub))
                continue;
            var go = stub.gameObject;
            var derived = go.GetComponent<PseudoPrefabCookingUtensilStub>();
            if (derived == null)
            {
                // 手动放置（未跑自动填充）：无派生 stub，复用 ApplyStub 补挂并登记。
                var item = new LayoutItemDto
                {
                    stubKind = "CookingUtensil",
                    cookingUtensil = new LayoutCookingUtensilStubDto
                    {
                        capacity = LayoutEditorStubIO.NativeUtensilCapacityForId(go.name),
                        allowedIngredientGuids = extraGuids.ToArray(),
                    },
                };
                LayoutEditorStubIO.ApplyStub(go, item);
                sceneDirty = true;
            }
            else if (MergeIngredients(derived, extra))
            {
                sceneDirty = true;
            }
        }
        if (sceneDirty)
        {
            foreach (var stub in Object.FindObjectsOfType<PseudoPrefabCookingUtensilStub>())
            {
                if (stub == null || stub.gameObject == null)
                    continue;
                var scene = stub.gameObject.scene;
                if (scene.IsValid())
                    EditorSceneManager.MarkSceneDirty(scene);
            }
        }
    }

    private static bool IsPushablePot(PseudoPrefabStub stub)
    {
        if (stub.gameObject == null)
            return false;
        if (IsTrack(stub))
            return false; // 轨道另行排除
        var n = stub.gameObject.name;
        if (n.IndexOf("pushable", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        var so = stub.pseudoPrefabSO;
        return so != null && so.prefabName != null &&
            so.prefabName.IndexOf("pushable", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>火锅轨道（名字含 large_pot 但不是锅）：不挂锅具 stub，
    /// 误挂时同样移除（flooredge 装饰条无 IngredientContainer，Setup NRE）。</summary>
    private static bool IsTrack(PseudoPrefabStub stub)
    {
        if (stub.gameObject == null)
            return false;
        if (stub.gameObject.name.IndexOf("track", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        var so = stub.pseudoPrefabSO;
        return so != null && so.prefabName != null &&
            so.prefabName.IndexOf("track", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsLargePot(PseudoPrefabStub stub)
    {
        if (stub.gameObject == null)
            return false;
        var n = stub.gameObject.name;
        if (IsTrack(stub))
            return false;
        // 可移动火锅（pushable）保留原版 PushableObject 机制，不挂 CookingUtensil stub
        // （其 child 无 IngredientContainer，宿主 Setup 会 NRE）。
        if (n.IndexOf("pushable", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return false;
        if (n.IndexOf("large_pot", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        var so = stub.pseudoPrefabSO;
        return so != null && so.prefabName != null &&
            so.prefabName.IndexOf("large_pot", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>把 extra 追加进 allowedIngredientSOs（去重，只增不删）。返回是否发生变更。</summary>
    private static bool MergeIngredients(PseudoPrefabCookingUtensilStub stub, List<ScriptableObject> extra)
    {
        if (stub.allowedIngredientSOs == null)
        {
            stub.allowedIngredientSOs = extra.ToArray();
            return true;
        }
        var existing = new HashSet<Object>();
        foreach (var so in stub.allowedIngredientSOs)
            if (so != null)
                existing.Add(so);
        var merged = new List<ScriptableObject>(stub.allowedIngredientSOs);
        bool changed = false;
        foreach (var so in extra)
        {
            if (so == null || existing.Contains(so))
                continue;
            merged.Add(so);
            existing.Add(so);
            changed = true;
        }
        if (!changed)
            return false;
        stub.allowedIngredientSOs = merged.ToArray();
        return true;
    }

    /// <summary>收集 LevelInfo 所选菜谱中 HotPot 步骤菜谱的叶食材 PseudoPrefabSO，
    ///  外加同 DLC 的 permutationsraw/cooked 中间产物（PseudoPrefabSORecipe，
    ///  位于 common03/Recipes/{dlc}/，Setup() 会按 PseudoPrefabSORecipe 分支加载节点）。</summary>
    public static List<ScriptableObject> CollectHotPotNodes(LevelInfoSO info)
    {
        var result = new List<ScriptableObject>();
        if (info == null || info.recipes == null)
            return result;
        var seen = new HashSet<Object>();
        var dlcPrefixes = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (var r in info.recipes)
        {
            if (r == null)
                continue;
            var path = AssetDatabase.GetAssetPath(r);
            var id = string.IsNullOrEmpty(path) ? null : System.IO.Path.GetFileNameWithoutExtension(path);
            string step;
            string[] ings;
            if (!LayoutEditorRecipeKnowledge.TryGetOriginal(id, out step, out ings))
                continue;
            if (step != "HotPot")
                continue;
            if (id != null && id.StartsWith("dlc", System.StringComparison.Ordinal))
            {
                // dlc10_hotpot_mixed → "dlc10_"；无前缀（dlc04 家族）→ ""。
                var under = id.IndexOf('_');
                if (under > 0)
                    dlcPrefixes.Add(id.Substring(0, under + 1));
                else
                    dlcPrefixes.Add("");
            }
            else
                dlcPrefixes.Add("");
            foreach (var ing in ings)
            {
                var so = LayoutEditorRoastTrayFill.LoadIngredientSo(ing);
                if (so != null && seen.Add(so))
                    result.Add(so);
            }
        }
        foreach (var prefix in dlcPrefixes)
        {
            foreach (var permId in new[] { prefix + "hotpot_permutationsraw", prefix + "hotpot_permutationscooked" })
            {
                var so = LoadRecipeSo(permId);
                if (so != null && seen.Add(so))
                    result.Add(so);
            }
        }
        return result;
    }

    /// <summary>在 common03/Recipes 下按 id 找 PseudoPrefabSO（permutations 等）。</summary>
    private static PseudoPrefabSO LoadRecipeSo(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;
        var root = "Assets/common03/Recipes";
        var absRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../" + root));
        if (!System.IO.Directory.Exists(absRoot))
            return null;
        var files = System.IO.Directory.GetFiles(absRoot, id + ".asset", System.IO.SearchOption.AllDirectories);
        if (files.Length == 0)
            return null;
        var rel = root + files[0].Substring(absRoot.Length).Replace('\\', '/');
        return AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(rel);
    }
}
