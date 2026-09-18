using System;
using System.Collections.Generic;
using System.IO;
using LevelEditorStub;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 「一键统一面包皮」后端（自定义菜谱页 /custom-recipes/{set} 的 🍞 按钮）。
///
/// 职责边界（刻意做窄，不要扩张）：
///  - **只改** Assets/LevelSets/&lt;set&gt;/custom_recipes/** 下自定义菜谱资产里的面包层：
///    普通菜谱改 compositionSOs 中命中 IsBurgerBunId 的元素；组装定义子类
///    （CustomRecipeOptionalBurgerSO）额外改 bunSO 字段（同在 custom_recipes 内的本地副本）。
///  - **不改** commonW2 共享库（全关卡集共用，改了污染所有集）、common01 官方库；
///    官方汉堡（Burger_Plain_SO 等）的面包锁死在游戏 bundle 内是 DLC02，物理改不了。
///  - **不负责**引用了这些菜谱的关卡：BurgerOptional.bunSO 重算、DLC 匹配表补全
///    （EnsureRequiredMatchlists）、bundle 依赖重建（EnsureWebDependencies）都在
///    POST /api/level-recipes 里，由关卡作者自行重存一次菜谱触发；场景食材箱同理由作者自行替换。
///    本端点只在返回值里给提醒。
///
/// 为什么不复用 POST /api/custom-recipes/update：UpdateCustomRecipe 是整体覆盖语义
/// （按 DTO 重设 type/cookingStep/模型变换，并清空非组装定义的 optionalSOs），
/// 拿它做批量换皮会连带清掉数据。这里只做定点替换，其余字段一律不碰。
/// </summary>
public static class LayoutEditorBunSwapApi
{
    /// <summary>bunSO 字段（组装定义）的 layerIndex 哨兵值，区别于 compositionSOs 的真实下标。</summary>
    public const int BunFieldLayerIndex = -1;

    // ------------------------------------------------------------ DTO

    [Serializable]
    public class BunUsageDto
    {
        public string assetPath;
        public string guid;
        /// <summary>菜谱资产文件名。</summary>
        public string id;
        public string nameZh;
        /// <summary>成品汉堡（Composite + 组成含面包）。</summary>
        public bool isFinishedBurger;
        /// <summary>组装定义子类（CustomRecipeOptionalBurgerSO）。</summary>
        public bool isAssembly;
        /// <summary>当前面包皮 id（ChoppedBunSO / DLC02_ChoppedBun / dlc08_choppedbun）。</summary>
        public string bunId;
        public string bunGuid;
        /// <summary>compositionSOs 下标；-1 = 组装定义的 bunSO 字段（BunFieldLayerIndex）。</summary>
        public int layerIndex;
    }

    [Serializable]
    public class BunUsageReportDto
    {
        /// <summary>可选目标面包皮（复用汉堡工作台的候选池，含 bundleName / bundleAvailable）。</summary>
        public LayoutEditorBurgerApi.BurgerCandidateDto[] buns;
        /// <summary>本关卡集 custom_recipes 内的面包层（可替换）。</summary>
        public BunUsageDto[] usages;
        /// <summary>commonW2 共享库内的面包层（只读展示，禁止替换）。</summary>
        public BunUsageDto[] sharedUsages;
        public string error;
    }

    [Serializable]
    public class BunReplaceRequestDto
    {
        public string setName;
        public string targetBunId;
        public string[] assetPaths;
        /// <summary>true = 只统计不落盘。</summary>
        public bool dryRun;
    }

    [Serializable]
    public class BunReplaceResultDto
    {
        /// <summary>实际改动的菜谱数。</summary>
        public int changed;
        /// <summary>实际改动的面包层数（一道菜谱可能有多层）。</summary>
        public int layers;
        public string[] recipes;
        /// <summary>被跳过的条目（含原因），路径闸门拦下的也在这里。</summary>
        public string[] skipped;
        public string[] warnings;
        public string error;
    }

    // ------------------------------------------------------------ 路径

    private static string Norm(string p)
    {
        if (string.IsNullOrEmpty(p))
            return "";
        return p.Replace('\\', '/');
    }

    internal static string SetRecipesDir(string setName)
    {
        return LayoutEditorLevelAdminApi.LevelSetsRoot + "/" + setName + "/custom_recipes";
    }

    /// <summary>路径闸门：只有本关卡集 custom_recipes/ 下的资产可写。
    ///  这是挡住 commonW2 / common01 共享库被误改的唯一防线，必须在后端做
    ///  （前端过滤不算数——端点可被直接调用）。</summary>
    internal static bool IsInSetCustomRecipes(string assetPath, string setName)
    {
        if (string.IsNullOrEmpty(assetPath) || string.IsNullOrEmpty(setName))
            return false;
        var prefix = Norm(SetRecipesDir(setName)) + "/";
        return Norm(assetPath).StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------ GET /api/custom-recipes/bun-usage

    /// <summary>扫描本关卡集（+ commonW2 只读）里全部面包层，供弹窗做替换预览。</summary>
    public static BunUsageReportDto GetUsage(string setName)
    {
        var dto = new BunUsageReportDto();
        dto.buns = LayoutEditorBurgerApi.CollectBuns();

        var local = new List<BunUsageDto>();
        if (!string.IsNullOrEmpty(setName))
            CollectUsageFrom(SetRecipesDir(setName), local);
        var shared = new List<BunUsageDto>();
        CollectUsageFrom(LayoutEditorLevelAdminApi.CommonW2RecipesDir, shared);

        dto.usages = local.ToArray();
        dto.sharedUsages = shared.ToArray();
        return dto;
    }

    private static void CollectUsageFrom(string dir, List<BunUsageDto> list)
    {
        if (!LayoutEditorLevelAdminApi.AssetFolderExists(dir))
            return;

        var zhMap = LayoutEditorLevelAdminApi.LoadCustomRecipeZhMap(dir);
        foreach (var asset in LayoutEditorLevelAdminApi.ScanCustomRecipeAssets(dir))
        {
            var so = AssetDatabase.LoadAssetAtPath<CustomRecipeSO>(asset.assetPath);
            if (so == null)
                continue;

            var fallbackId = Path.GetFileNameWithoutExtension(asset.assetPath);
            var nameZh = ResolveNameZh(zhMap, so, fallbackId);
            var isFinished = LayoutEditorBurgerApi.IsFinishedBurgerRecipe(so);
            var assembly = so as CustomRecipeOptionalBurgerSO;

            var comps = so.compositionSOs;
            if (comps != null)
            {
                for (int i = 0; i < comps.Length; i++)
                {
                    string bunId, bunGuid;
                    if (!TryReadBun(comps[i], out bunId, out bunGuid))
                        continue;
                    list.Add(MakeUsage(asset, fallbackId, nameZh, isFinished, assembly != null, bunId, bunGuid, i));
                }
            }

            // 组装定义的 bunSO（关卡集本地副本）：不换会与成品汉堡的面包层脱节。
            if (assembly != null)
            {
                string bunId, bunGuid;
                if (TryReadBun(assembly.bunSO, out bunId, out bunGuid))
                    list.Add(MakeUsage(asset, fallbackId, nameZh, isFinished, true, bunId, bunGuid, BunFieldLayerIndex));
            }
        }

        list.Sort(CompareUsage);
    }

    private static int CompareUsage(BunUsageDto a, BunUsageDto b)
    {
        var byName = string.Compare(a.nameZh, b.nameZh, StringComparison.Ordinal);
        if (byName != 0)
            return byName;
        return a.layerIndex.CompareTo(b.layerIndex);
    }

    private static BunUsageDto MakeUsage(
        LayoutEditorLevelAdminApi.AssetRef asset,
        string id,
        string nameZh,
        bool isFinishedBurger,
        bool isAssembly,
        string bunId,
        string bunGuid,
        int layerIndex)
    {
        return new BunUsageDto
        {
            assetPath = asset.assetPath,
            guid = asset.guid,
            id = id,
            nameZh = nameZh,
            isFinishedBurger = isFinishedBurger,
            isAssembly = isAssembly,
            bunId = bunId,
            bunGuid = bunGuid,
            layerIndex = layerIndex,
        };
    }

    /// <summary>组成项/bunSO 是不是面包皮；是则回填 id 与 guid。</summary>
    private static bool TryReadBun(ScriptableObject so, out string bunId, out string bunGuid)
    {
        bunId = null;
        bunGuid = null;
        if (so == null)
            return false;
        var p = AssetDatabase.GetAssetPath(so);
        if (string.IsNullOrEmpty(p))
            return false;
        var id = Path.GetFileNameWithoutExtension(p);
        if (!LayoutEditorBurgerApi.IsBurgerBunId(id))
            return false;
        bunId = id;
        bunGuid = AssetDatabase.AssetPathToGUID(p);
        return true;
    }

    private static string ResolveNameZh(Dictionary<string, string> zhMap, CustomRecipeSO so, string fallbackId)
    {
        var key = !string.IsNullOrEmpty(so.recipeName) ? so.recipeName : fallbackId;
        string zh;
        if (zhMap.TryGetValue(key, out zh) && !string.IsNullOrEmpty(zh))
            return zh;
        if (zhMap.TryGetValue(fallbackId, out zh) && !string.IsNullOrEmpty(zh))
            return zh;
        return fallbackId;
    }

    // ------------------------------------------------------------ POST /api/custom-recipes/replace-bun

    /// <summary>把选中菜谱里的面包层原位替换成目标面包皮（保持顺序与层数）。</summary>
    public static BunReplaceResultDto Replace(BunReplaceRequestDto req)
    {
        var res = new BunReplaceResultDto();
        res.recipes = new string[0];
        res.skipped = new string[0];
        res.warnings = new string[0];

        if (req == null || string.IsNullOrEmpty(req.setName))
        {
            res.error = "缺少关卡集名。";
            return res;
        }
        if (string.IsNullOrEmpty(req.targetBunId))
        {
            res.error = "缺少目标面包皮。";
            return res;
        }
        if (!LayoutEditorBurgerApi.IsBurgerBunId(req.targetBunId))
        {
            res.error = "目标不是汉堡面包皮：" + req.targetBunId;
            return res;
        }

        // 目标校验：必须在面包候选池内，且 bundle 已在 StreamingAssets
        // （铁律「只注册存在的 bundle」——缺包的面包换上去运行时 GetAssetBundle 会抛）。
        PseudoPrefabSO target = null;
        var targetBundle = "";
        foreach (var b in LayoutEditorBurgerApi.CollectBuns())
        {
            if (!string.Equals(b.id, req.targetBunId, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!b.bundleAvailable)
            {
                res.error = "目标面包皮的 bundle 未就绪（" + b.bundleName + "），换上去运行时会崩，已中止。";
                return res;
            }
            target = AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(b.assetPath);
            targetBundle = b.bundleName ?? "";
            break;
        }
        if (target == null)
        {
            res.error = "未找到目标面包皮资产：" + req.targetBunId;
            return res;
        }

        var targetPath = AssetDatabase.GetAssetPath(target);
        var targetId = Path.GetFileNameWithoutExtension(targetPath);

        var paths = req.assetPaths;
        if (paths == null || paths.Length == 0)
        {
            res.error = "没有选择要替换的菜谱。";
            return res;
        }

        var changedIds = new List<string>();
        var skipped = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var layerCount = 0;

        foreach (var raw in paths)
        {
            var p = Norm(raw);
            if (string.IsNullOrEmpty(p) || !seen.Add(p))
                continue;

            // 路径闸门：commonW2 / common01 共享库与关卡集外资产一律拒绝。
            if (!IsInSetCustomRecipes(p, req.setName))
            {
                skipped.Add(p + "：不在 " + SetRecipesDir(req.setName) + "/ 内，已拒绝");
                continue;
            }

            var so = AssetDatabase.LoadAssetAtPath<CustomRecipeSO>(p);
            if (so == null)
            {
                skipped.Add(p + "：资产不存在或不是自定义菜谱");
                continue;
            }

            var hits = new List<int>();
            var comps = so.compositionSOs;
            if (comps != null)
            {
                for (int i = 0; i < comps.Length; i++)
                {
                    string bunId, bunGuid;
                    if (!TryReadBun(comps[i], out bunId, out bunGuid))
                        continue;
                    if (string.Equals(bunId, targetId, StringComparison.OrdinalIgnoreCase))
                        continue; // 已是目标，不重复写
                    hits.Add(i);
                }
            }

            var assembly = so as CustomRecipeOptionalBurgerSO;
            var swapBunField = false;
            if (assembly != null)
            {
                string bunId, bunGuid;
                if (TryReadBun(assembly.bunSO, out bunId, out bunGuid)
                    && !string.Equals(bunId, targetId, StringComparison.OrdinalIgnoreCase))
                    swapBunField = true;
            }

            if (hits.Count == 0 && !swapBunField)
            {
                skipped.Add(p + "：没有需要替换的面包层");
                continue;
            }

            if (!req.dryRun)
            {
                Undo.RecordObject(so, "Replace Burger Bun");
                // 原位替换：保持 compositionSOs 的顺序与长度不变（comps 就是字段本身的引用）。
                for (int k = 0; k < hits.Count; k++)
                    comps[hits[k]] = target;
                if (swapBunField)
                    assembly.bunSO = target;
                EditorUtility.SetDirty(so);
            }

            layerCount += hits.Count + (swapBunField ? 1 : 0);
            changedIds.Add(Path.GetFileNameWithoutExtension(p));
        }

        if (!req.dryRun && changedIds.Count > 0)
            AssetDatabase.SaveAssets();

        res.changed = changedIds.Count;
        res.layers = layerCount;
        res.recipes = changedIds.ToArray();
        res.skipped = skipped.ToArray();
        res.warnings = BuildWarnings(targetId, targetBundle, changedIds.Count).ToArray();

        if (!req.dryRun)
        {
            LayoutEditorLog.Log("[BunSwap] " + req.setName + "：已把 " + changedIds.Count
                + " 道自定义菜谱的 " + layerCount + " 个面包层换成 " + targetId
                + "（bundle=" + targetBundle + "）");
        }
        return res;
    }

    /// <summary>提醒文案（只提醒，不做任何自动联动）。</summary>
    private static List<string> BuildWarnings(string targetId, string targetBundle, int changed)
    {
        var w = new List<string>();
        if (string.Equals(targetId, "DLC02_ChoppedBun", StringComparison.OrdinalIgnoreCase))
        {
            w.Add("DLC2 面皮未经 IngredientOrderNode uID 实测，不在核心面包/DLC8 面皮的等价组内"
                + "（recipeGroups.ts BUN_EQUIVALENT_IDS），已有食材箱可能接不上订单。");
        }
        if (changed <= 0)
            return w;

        w.Add("本次只改了自定义菜谱资产。引用这些菜谱的关卡需各重存一次菜谱，才会重算"
            + " BurgerOptional.bunSO、补全 DLC 匹配表、重建 bundle 依赖（"
            + (string.IsNullOrEmpty(targetBundle) ? "目标 bundle 未知" : "目标 bundle " + targetBundle) + "）。");
        w.Add("场景里的食材箱不在本次替换范围内，请自行核对是否还给着旧面包皮。");
        return w;
    }
}
