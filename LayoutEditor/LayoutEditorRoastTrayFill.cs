using System.Collections.Generic;
using LevelEditorStub;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 烤菜烤盘「默认能放」：把所选烤菜菜谱的叶食材自动登记为烤盘额外食材。
///
/// 背景：编辑器推荐/放置的基础版烤盘（utensil_roasting_tray，dlc07/bundle297）内置
/// m_approvedContentsList（DLC07_RoastingTrayObjectsLookup）只含基础/dlc07 烤菜食材节点
/// （Beef_Roast uID 54456、Chicken_Roast 32308 等）；dlc09 烤菜食材（dlc09_beef_roast
/// 等，uID 380152/380168/380160/380188/380176）是另一组独立节点。未登记时运行时
/// CookableContainer.AllowItemPlacement 查 GetPrefabForNode 找不到节点而拒绝放入。
/// 只有前端「自动填充道具」才写 allowedIngredientGuids，手动放置烤盘时不登记 →
/// 进游戏放不进去。
///
/// 处理：保存布局（SceneLayoutApplier.Apply）与保存菜谱（SetLevelRecipes）后，把
/// LevelInfo 所选菜谱中 RoastingTray 步骤菜谱的叶食材（PseudoPrefabSO）登记为场景里
/// 所有烤盘 stub 的 allowedIngredientSOs（只增不删，幂等）：
///  - 已有派生 stub（自动填充过）→ 直接追加 allowedIngredientSOs；
///  - 只有基础 stub（手动放置）→ 复用 LayoutEditorStubIO.ApplyStub 补挂派生
///    stub + 派生运行时组件（PseudoPrefabCookingUtensil.Setup 才会重建
///    approvedContents），并写入叶食材。
/// 按关卡实际所选菜谱取食材，dlc07 与 dlc09 都覆盖，不依赖前端时机。宿主文件一律不动。
/// </summary>
public static class LayoutEditorRoastTrayFill
{
    /// <summary>把所选烤菜菜谱的叶食材合并进场景所有烤盘 stub 的 allowedIngredientSOs（只增不删）。</summary>
    public static void EnsureRoastTrayIngredients(LevelInfoSO info)
    {
        if (info == null)
            return;
        var extra = CollectRoastTrayIngredients(info);
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
            if (stub == null || stub.gameObject == null || !IsRoastTray(stub))
                continue;
            var go = stub.gameObject;
            var derived = go.GetComponent<PseudoPrefabCookingUtensilStub>();
            if (derived == null)
            {
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

    /// <summary>保存菜谱时覆盖同步：仅保留当前 LevelInfo 烤菜菜谱所需食材，移除无关遗留项。</summary>
    public static void SyncRoastTrayIngredients(LevelInfoSO info)
    {
        if (info == null)
            return;
        var required = CollectRoastTrayIngredients(info);
        ApplyTrayIngredients(required, IsRoastTray);
    }

    private static void ApplyTrayIngredients(
        List<PseudoPrefabSO> required,
        System.Func<PseudoPrefabStub, bool> isTray)
    {
        bool sceneDirty = false;
        foreach (var stub in Object.FindObjectsOfType<PseudoPrefabStub>())
        {
            if (stub == null || stub.gameObject == null || !isTray(stub))
                continue;
            var go = stub.gameObject;
            var derived = go.GetComponent<PseudoPrefabCookingUtensilStub>();
            if (derived == null)
            {
                if (required.Count == 0)
                    continue;
                var extraGuids = new List<string>();
                foreach (var so in required)
                {
                    var p = AssetDatabase.GetAssetPath(so);
                    if (!string.IsNullOrEmpty(p))
                        extraGuids.Add(AssetDatabase.AssetPathToGUID(p));
                }
                if (extraGuids.Count == 0)
                    continue;
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
            else if (SetIngredients(derived, required))
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

    private static bool IsRoastTray(PseudoPrefabStub stub)
    {
        if (stub.gameObject == null)
            return false;
        if (stub.gameObject.name.IndexOf("roasting_tray", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        var so = stub.pseudoPrefabSO;
        return so != null && so.prefabName != null &&
            so.prefabName.IndexOf("roasting_tray", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>把 extra 覆盖写入 allowedIngredientSOs。返回是否发生变更。</summary>
    private static bool SetIngredients(PseudoPrefabCookingUtensilStub stub, List<PseudoPrefabSO> required)
    {
        var next = required.ToArray();
        var cur = stub.allowedIngredientSOs;
        if (cur != null && cur.Length == next.Length)
        {
            bool same = true;
            for (int i = 0; i < cur.Length; i++)
            {
                if (cur[i] != next[i])
                {
                    same = false;
                    break;
                }
            }
            if (same)
                return false;
        }
        stub.allowedIngredientSOs = next;
        return true;
    }

    /// <summary>（场景写回）把 extra 追加进 allowedIngredientSOs（去重，只增不删）。</summary>
    private static bool MergeIngredients(PseudoPrefabCookingUtensilStub stub, List<PseudoPrefabSO> extra)
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

    /// <summary>收集 LevelInfo 所选菜谱中 RoastingTray 步骤菜谱的叶食材 PseudoPrefabSO
    ///  （从 common03/common01/common02 本地源库按 id 解析；dlc07/dlc09 都覆盖）。</summary>
    public static List<PseudoPrefabSO> CollectRoastTrayIngredients(LevelInfoSO info)
    {
        var result = new List<PseudoPrefabSO>();
        if (info == null || info.recipes == null)
            return result;
        var seen = new HashSet<Object>();
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
            if (step != "RoastingTray")
                continue;
            foreach (var ing in ings)
            {
                var so = LoadIngredientSo(ing);
                if (so != null && seen.Add(so))
                    result.Add(so);
            }
        }
        return result;
    }

    public static PseudoPrefabSO LoadIngredientSo(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;
        // 优先 common03（dlc 换皮）；基础食材（如 CarrotSO）在 common01/common02。
        // 与 LayoutEditorCustomIngredients.IsLocalSourceAsset 一致，只查本地源库。
        string[] roots =
        {
            "Assets/common03/Ingredients",
            "Assets/common01/food/Ingredients",
            "Assets/common02/food/Ingredients",
        };
        foreach (var root in roots)
        {
            var absRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../" + root));
            if (!System.IO.Directory.Exists(absRoot))
                continue;
            var files = System.IO.Directory.GetFiles(absRoot, id + ".asset", System.IO.SearchOption.AllDirectories);
            if (files.Length == 0)
                continue;
            var rel = root + files[0].Substring(absRoot.Length).Replace('\\', '/');
            var so = AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(rel);
            if (so != null)
                return so;
        }
        return null;
    }
}
