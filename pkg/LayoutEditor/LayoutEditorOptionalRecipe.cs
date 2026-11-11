using System.Collections.Generic;
using LevelEditorStub;
using UnityEngine;

/// <summary>
/// 可选组装菜谱（ICustomRecipeOptional）统一识别/遍历。
///
/// 2026-11 上游把 burger/pizza 的硬编码分支收敛为 ICustomRecipeOptional，并新增
/// CustomRecipeOptionalSandwichSO。布局编辑器原先到处写
/// `so is CustomRecipeOptionalBurgerSO ? ... : so is CustomRecipeOptionalPizzaSO ? ...`，
/// 这里集中判断，后续上游再加 optional 类型时只需要改 KindOf 和遍历字段。
/// </summary>
public static class LayoutEditorOptionalRecipe
{
    /// <summary>Assets/Scripts/LevelEditorStub/CustomRecipeOptionalSandwichSO.cs.meta</summary>
    public const string SandwichScriptGuid = "1dceb690bde9e10439e48644df81e9c9";

    public static bool IsAssembly(CustomRecipeSO so)
    {
        return so is ICustomRecipeOptional;
    }

    public static bool IsAssemblyKind(string kind)
    {
        return kind == "burger" || kind == "pizza" || kind == "sandwich";
    }

    public static string KindOf(CustomRecipeSO so)
    {
        if (so is CustomRecipeOptionalBurgerSO) return "burger";
        if (so is CustomRecipeOptionalPizzaSO) return "pizza";
        if (so is CustomRecipeOptionalSandwichSO) return "sandwich";
        return "";
    }

    /// <summary>可选组装定义自身引用的 SO（用于 allIngredients / 依赖收集 / 组成遍历）。
    ///  与普通 CustomRecipeSO 的 compositionSOs/optionalSOs 语义不同，调用方不要重复追加。</summary>
    public static void CollectAssemblySoRefs(CustomRecipeSO so, List<ScriptableObject> refs)
    {
        if (so == null || refs == null)
            return;
        var burger = so as CustomRecipeOptionalBurgerSO;
        if (burger != null)
        {
            Add(refs, burger.bunSO);
            AddRange(refs, burger.ingredientModelSOs);
            return;
        }
        var pizza = so as CustomRecipeOptionalPizzaSO;
        if (pizza != null)
        {
            Add(refs, pizza.doughSO);
            AddRange(refs, pizza.rawPizzaIngredientPrefabSOs);
            AddRange(refs, pizza.cookedPizzaIngredientPrefabSOs);
            return;
        }
        var sandwich = so as CustomRecipeOptionalSandwichSO;
        if (sandwich != null)
        {
            Add(refs, sandwich.toastSO);
            AddRange(refs, sandwich.optionalSOs);
            AddRange(refs, sandwich.sauceSOs);
            AddRange(refs, sandwich.ingredientModelSOs);
        }
    }

    /// <summary>可选组装定义中的“食材”引用（不含模型 SO）。burger/pizza 保持旧行为
    /// 不参与组成统计；sandwich 把 toast/夹心/酱料计入，供菜谱卡与组成 ids 使用。</summary>
    public static void CollectAssemblyIngredientRefs(CustomRecipeSO so, List<ScriptableObject> refs)
    {
        if (so == null || refs == null)
            return;
        var sandwich = so as CustomRecipeOptionalSandwichSO;
        if (sandwich == null)
            return;
        Add(refs, sandwich.toastSO);
        AddRange(refs, sandwich.optionalSOs);
        AddRange(refs, sandwich.sauceSOs);
    }

    private static void Add(List<ScriptableObject> refs, ScriptableObject so)
    {
        if (so != null && !refs.Contains(so))
            refs.Add(so);
    }

    private static void AddRange(List<ScriptableObject> refs, ScriptableObject[] list)
    {
        if (list == null)
            return;
        for (int i = 0; i < list.Length; i++)
            Add(refs, list[i]);
    }
}
