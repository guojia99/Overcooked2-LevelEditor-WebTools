using System;
using System.Collections.Generic;
using System.IO;
using LevelEditorStub;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 汉堡组装工作台后端（web #/burger-maker 页面的 /api/burger/* 端点实现）。
///
/// 数据模型（详见 Docs/zh/reference.md「Burger大全」）：
///  - 成品汉堡 = 普通 CustomRecipeSO（type=Composite，compositionSOs = 面包 + 夹心层），
///    无整体模型，modelSO 指向官方空壳组装 prefab（CompositeBurger）。
///  - 组装外观由共享的 CustomRecipeOptionalBurgerSO 决定：bunSO（面包底座）
///    + optionalSOs[]（可选夹心，重复项 = 同种允许叠多层）
///    + ingredientModelSOs[] / ingredientModels[]（与 optionalSOs 按下标一一对齐的堆叠模型）。
///
/// 所有资产都在 commonW2 共享库（LayoutEditorLevelAdminApi.CommonW2RecipesDir）中。
/// </summary>
public static class LayoutEditorBurgerApi
{
    /// <summary>与 LevelInfo 同目录的本关汉堡夹心 optional 配置（CustomRecipeOptionalBurgerSO）。</summary>
    internal const string LevelBurgerOptionalFileName = "BurgerOptional";

    // ------------------------------------------------------------ DTO

    [Serializable]
    public class BurgerLayerDto
    {
        public string guid;          // optionalSO 条目 guid
        public string id;            // 资产文件名（食材/中间产物 id）
        public string nameZh;        // names.json 中文名（无则回退 id）
        public string kind;          // "custom"（自定义中间产物）/ "official-recipe" / "ingredient"
        public string modelId;       // ingredientModelSOs[i] 的 PseudoPrefabSO 文件名（"" = 未绑定，运行时回退官方 lookup）
        public string modelAssetPath;
        public bool hasDirectModel;  // ingredientModels[i] 直接引用 GameObject
    }

    [Serializable]
    public class BurgerDefinitionDto
    {
        public string assetPath;
        public string guid;
        public string id;
        public string nameZh;
        public string bunId;
        public int capacity;             // ingredientContainerCapacity（单个汉堡最多夹心层数）
        public BurgerLayerDto[] layers;  // optionalSOs（含重复，重复 = 允许多层）
        /// <summary>true = 关卡集本地副本（custom_recipes 内），false = commonW2 共享定义。</summary>
        public bool isLocal;
    }

    [Serializable]
    public class BurgerCandidateDto
    {
        public string guid;
        public string id;
        public string nameZh;
        public string nameEn;
        public string kind;      // "bun" / "custom"（自定义中间产物/成品）/ "official-recipe" / "ingredient"
        public string assetPath; // custom 类候选的图标走 /api/custom-recipes/icon?assetPath=
        /// <summary>堆叠模型状态：
        ///  "bound"   = commonW2 组装定义里已有绑定；
        ///  "own"     = 菜谱自带模型（CustomRecipeSO.model / modelSO），由 ResolveLayerModel 第三级兜底生效；
        ///  "none"    = 三处都没有 —— 官方生食材属设计内（运行时回退官方面包 lookup），
        ///              自定义菜谱则会导致叠层不可见，需要补模型。</summary>
        public string modelState;
        /// <summary>图标状态："own"（专属图标）/ "generic"（共享占位图，需降级到食材图标）/ "none"。</summary>
        public string iconState;
        /// <summary>叶食材 id（递归展开）：iconState != "own" 时前端按此顺序回退取食材图标。</summary>
        public string[] iconFallbackIds;
        /// <summary>该候选所属 DLC 匹配表 key（dlc05 等；"" = 本传/无需额外匹配表）。
        ///  保存菜谱时由 LayoutEditorMatchlistMap 自动补入，这里仅作展示。</summary>
        public string matchlistKey;
        /// <summary>指向的游戏 bundle（PseudoPrefabSO 才有）。</summary>
        public string bundleName;
        /// <summary>bundle 是否已在 StreamingAssets —— false 时禁止作为夹心（运行时会崩）。</summary>
        public bool bundleAvailable;
        /// <summary>是否在 commonW2 汉堡大全的既有夹心白名单内（推荐项，UI 置顶）。</summary>
        public bool recommended;
        /// <summary>自定义菜谱分数（0 = 中间产物）。</summary>
        public int score;
        /// <summary>该候选有本地模型文件（models/&lt;Id&gt;/*.fbx|obj），可在网页 3D 预览。</summary>
        public bool previewable;
    }

    [Serializable]
    public class BurgerDefinitionListDto
    {
        public BurgerDefinitionDto[] definitions;
        /// <summary>夹心候选：commonW2/common01 中间产物 + 官方食材 + 官方成品菜。</summary>
        public BurgerCandidateDto[] candidates;
        /// <summary>汉堡面包候选（各 DLC ChoppedBun 等）。</summary>
        public BurgerCandidateDto[] buns;
        /// <summary>可绑定的堆叠模型（commonW2 models 目录内的 PseudoPrefabSO 指针）。</summary>
        public BurgerModelDto[] models;
        /// <summary>已有成品汉堡（供工作台"载入堆叠"继续编辑/复制改层）。</summary>
        public BurgerProductDto[] products;
    }

    [Serializable]
    public class BurgerProductDto
    {
        public string guid;
        public string id;
        public string assetPath;
        public string recipeName;
        public string nameZh;
        /// <summary>names.json 里的英文名（缺失时前端回退 recipeName）。</summary>
        public string nameEn;
        public int score;
        /// <summary>compositionSOs 的资产 id 列表（含面包底）。</summary>
        public string[] compositionIds;
    }

    [Serializable]
    public class BurgerModelDto
    {
        public string id;
        /// <summary>包装 SO 的资产路径；"" = 池条目尚未生成包装（选用时按需生成）。</summary>
        public string assetPath;
        public string prefabName;
        public string bundleName;
        /// <summary>"local" = commonW2/models 已有包装；"pool" = dump_bundle 提取的原版模型池。</summary>
        public string source;
        /// <summary>bundle 内实路径（池条目才有），大小写敏感。</summary>
        public string bundleAssetPath;
        public string dlc;
        /// <summary>名字含 plated/prep/recipe —— 通常是「摆在容器里的成品形态」，最适合当堆叠层。</summary>
        public bool stackable;
    }

    [Serializable]
    public class BurgerCreateDto
    {
        public string setName;             // 目标关卡集（产出归属：该关卡集的普通自定义菜谱）
        public string category;            // 关卡集内分类 id（空 = "burger_local"）
        public string definitionAssetPath; // 参考的 commonW2 组装定义（只读：modelSO / platingStepSO）
        public string recipeName;          // ASCII id（全局唯一）
        public string nameZh;
        public string nameEn;
        public int score;
        public string[] layerIds;          // 夹心层 id（顺序 = 堆叠顺序，面包自动置底）
        public string iconBase64;          // 可选：订单图标 png
        /// <summary>非空 = 更新已有成品汉堡。</summary>
        public string updateAssetPath;
    }

    [Serializable]
    public class BurgerCreateResultDto
    {
        public bool ok;
        public string error;
        public string assetPath;
        public string guid;
        public int uID;
        public string[] addedToDefinition;
        public bool updated;
        public string iconError;
        /// <summary>非阻断告警（无堆叠模型 / 需要 DLC 匹配表等），前端保存后展示。</summary>
        public string[] warnings;
    }

    [Serializable]
    public class BurgerModelBindingDto
    {
        public string layerGuid; // optionalSO 条目 guid（该食材的全部出现一并绑定）
        public string modelId;   // 模型 id；"" = 解绑（回退菜谱自带模型 / 官方 lookup）
        /// <summary>模型通道："pseudo"（默认，bundle 指针 → ingredientModelSOs）/
        ///  "local"（commonW2 本地 prefab → ingredientModels）。两者互斥，绑一个必清另一个。</summary>
        public string modelKind;
    }

    [Serializable]
    public class BurgerDefinitionUpdateDto
    {
        public string assetPath;
        public int capacity;                  // >0 时更新 ingredientContainerCapacity
        public string[] addLayerIds;          // 追加可选夹心种类（模型位继承 commonW2 绑定）
        public string[] removeLayerGuids;     // 按 guid 移除该食材的全部出现
        public BurgerModelBindingDto[] modelBindings;
    }

    // ------------------------------------------------------------ 查询

    /// <summary>GET /api/burger/definitions?setName=&amp;includeModelless=：commonW2 共享组装定义（工作台参考壳）。
    ///  includeModelless=1 时放行「无堆叠模型」的自定义菜谱候选（默认过滤，见 CollectCandidates）。</summary>
    public static BurgerDefinitionListDto GetDefinitions(string setName, bool includeModelless)
    {
        var list = new List<BurgerDefinitionDto>();
        var dir = LayoutEditorLevelAdminApi.CommonW2RecipesDir;
        if (LayoutEditorLevelAdminApi.AssetFolderExists(dir))
        {
            var zhMap = LayoutEditorLevelAdminApi.LoadCustomRecipeZhMap(dir);
            foreach (var asset in LayoutEditorLevelAdminApi.ScanAssetsByScript(
                         dir, LayoutEditorLevelAdminApi.OptionalBurgerScriptGuid))
            {
                var so = AssetDatabase.LoadAssetAtPath<CustomRecipeOptionalBurgerSO>(asset.assetPath);
                if (so == null)
                    continue;
                list.Add(BuildDefinitionDto(so, asset.assetPath, asset.guid, zhMap));
            }
        }
        list.Sort((a, b) => string.Compare(a.id, b.id, StringComparison.Ordinal));
        return new BurgerDefinitionListDto
        {
            definitions = list.ToArray(),
            candidates = CollectCandidates(setName, includeModelless),
            buns = CollectBuns(),
            models = CollectModels(),
            products = CollectProducts(setName),
        };
    }

    /// <summary>汉堡面包 id 判定：核心 ChoppedBunSO / DLC02_ChoppedBun /
    /// DLC8 dlc08_choppedbun 三种写法都含 "ChoppedBun"。
    /// 历史别名 dlc08_bun（commonW1 装饰壳旧文件名）与 DLC08_ChoppedBun（旧资产
    /// 文件名）已于命名统一时改名到 dlc08_choppedbun，不再需要特判。</summary>
    internal static bool IsBurgerBunId(string id)
    {
        if (string.IsNullOrEmpty(id))
            return false;
        if (id.IndexOf("ChoppedBun", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        return false;
    }

    /// <summary>成品汉堡：Composite 且 composition 含面包层。</summary>
    internal static bool IsFinishedBurgerRecipe(CustomRecipeSO so)
    {
        if (so == null || so.type != CustomRecipeSO.RecipeType.Composite)
            return false;
        foreach (var c in so.compositionSOs ?? new ScriptableObject[0])
        {
            if (c == null)
                continue;
            var cp = AssetDatabase.GetAssetPath(c);
            if (string.IsNullOrEmpty(cp))
                continue;
            if (IsBurgerBunId(Path.GetFileNameWithoutExtension(cp)))
                return true;
        }
        return false;
    }

    internal static string LevelBurgerOptionalAssetPath(string levelInfoAssetPath)
    {
        if (string.IsNullOrEmpty(levelInfoAssetPath))
            return null;
        var dir = Path.GetDirectoryName(levelInfoAssetPath.Replace('\\', '/'));
        if (string.IsNullOrEmpty(dir))
            return null;
        return dir + "/" + LevelBurgerOptionalFileName + ".asset";
    }

    internal static CustomRecipeOptionalBurgerSO FindLevelBurgerOptional(string levelInfoAssetPath)
    {
        var path = LevelBurgerOptionalAssetPath(levelInfoAssetPath);
        if (string.IsNullOrEmpty(path))
            return null;
        return AssetDatabase.LoadAssetAtPath<CustomRecipeOptionalBurgerSO>(path);
    }

    /// <summary>确保 data/{level}/BurgerOptional.asset 存在（从 commonW2 模板复制壳）。</summary>
    internal static string FindOrCreateLevelBurgerOptional(
        string levelInfoAssetPath,
        string levelSet,
        out CustomRecipeOptionalBurgerSO optional)
    {
        optional = null;
        var assetPath = LevelBurgerOptionalAssetPath(levelInfoAssetPath);
        if (string.IsNullOrEmpty(assetPath))
            return "无效的 LevelInfo 路径。";

        optional = AssetDatabase.LoadAssetAtPath<CustomRecipeOptionalBurgerSO>(assetPath);
        if (optional != null)
            return null;

        var template = FindCommonW2BurgerModelAssembly();
        if (template == null)
            return "未找到 commonW2 汉堡组装模板（ChickenBurgerAssembly / OptionalBurger）。";
        var templatePath = AssetDatabase.GetAssetPath(template);
        if (string.IsNullOrEmpty(templatePath))
            return "汉堡组装模板路径无效。";

        CustomRecipeConfigSO config = null;
        string recipesDir = null;
        if (!string.IsNullOrEmpty(levelSet))
        {
            recipesDir = LayoutEditorLevelAdminApi.LevelSetsRoot + "/" + levelSet + "/custom_recipes";
            if (LayoutEditorLevelAdminApi.AssetFolderExists(recipesDir))
                config = AssetDatabase.LoadAssetAtPath<CustomRecipeConfigSO>(recipesDir + "/CustomRecipeConfig.asset");
        }

        if (!AssetDatabase.CopyAsset(templatePath, assetPath))
            return "创建本关 BurgerOptional 失败：" + assetPath;

        optional = AssetDatabase.LoadAssetAtPath<CustomRecipeOptionalBurgerSO>(assetPath);
        if (optional == null)
            return "本关 BurgerOptional 加载失败。";

        optional.recipeName = LevelBurgerOptionalFileName;
        optional.optionalSOs = new ScriptableObject[0];
        optional.ingredientModelSOs = new PseudoPrefabSO[0];
        optional.ingredientModels = new GameObject[0];
        optional.ingredientContainerCapacity = 1;

        if (config != null)
        {
            int uid;
            do
            {
                uid = config.uidPrefix * 1000 + config.nextSequence;
                config.nextSequence++;
            } while (LayoutEditorLevelAdminApi.IsUidConflicting(uid));
            optional.uID = uid;
            EditorUtility.SetDirty(config);
            LayoutEditorLevelAdminApi.AddCustomRecipeName(
                LayoutEditorLevelAdminApi.CustomRecipeNamesPath(null, assetPath),
                LevelBurgerOptionalFileName, "本关汉堡夹心", "BurgerOptional");
        }

        EditorUtility.SetDirty(optional);
        AssetDatabase.SaveAssets();
        LayoutEditorLog.Log("[Optional] 已创建本关 BurgerOptional：" + assetPath);
        return null;
    }

    internal static bool BurgerOptionalHasFillerLayers(CustomRecipeOptionalBurgerSO optional)
    {
        if (optional == null)
            return false;
        var opts = optional.optionalSOs;
        return opts != null && opts.Length > 0;
    }

    /// <summary>commonW2 组装定义的夹心模型绑定索引：optionalSO 的 guid → 堆叠模型。
    /// OptionalBurger 主模板（绑定最全）优先，其余 *Assembly/_filler 补充，首个非空获胜。</summary>
    /// <summary>commonW2 汉堡库根目录（burger/，其下按子分类分目录：
    ///  assembly / classic / deluxe / mega / breakfast / seafood / veggie / filling
    ///  + 共享的 models/ 与 icons/）。</summary>
    internal static string CommonW2BurgerDir()
    {
        return LayoutEditorLevelAdminApi.CommonW2RecipesDir + "/"
            + LayoutEditorLevelAdminApi.BurgerCategoryId;
    }

    /// <summary>按文件名在 commonW2 汉堡库内递归查找组装定义（CustomRecipeOptionalBurgerSO）。
    ///  2026-09-15 细分类迁移后组装定义落在 burger/assembly/，这里用递归扫描而不是硬编码
    ///  二级目录，将来再调整分类也不用改代码。</summary>
    private static CustomRecipeOptionalBurgerSO FindCommonW2Assembly(string id)
    {
        var burgerDir = CommonW2BurgerDir();
        if (string.IsNullOrEmpty(id) || !LayoutEditorLevelAdminApi.AssetFolderExists(burgerDir))
            return null;
        foreach (var asset in LayoutEditorLevelAdminApi.ScanAssetsByScript(
                     burgerDir, LayoutEditorLevelAdminApi.OptionalBurgerScriptGuid))
        {
            if (string.Equals(Path.GetFileNameWithoutExtension(asset.assetPath), id, StringComparison.Ordinal))
                return AssetDatabase.LoadAssetAtPath<CustomRecipeOptionalBurgerSO>(asset.assetPath);
        }
        return null;
    }

    internal static void CollectCommonW2LayerModelBindings(
        out Dictionary<string, PseudoPrefabSO> modelByGuid,
        out Dictionary<string, GameObject> modelGoByGuid)
    {
        modelByGuid = new Dictionary<string, PseudoPrefabSO>(StringComparer.Ordinal);
        modelGoByGuid = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        var burgerDir = CommonW2BurgerDir();
        if (!LayoutEditorLevelAdminApi.AssetFolderExists(burgerDir))
            return;

        var ordered = new List<CustomRecipeOptionalBurgerSO>();
        var master = FindCommonW2Assembly("OptionalBurger");
        if (master != null)
            ordered.Add(master);
        foreach (var asset in LayoutEditorLevelAdminApi.ScanAssetsByScript(
                     burgerDir, LayoutEditorLevelAdminApi.OptionalBurgerScriptGuid))
        {
            var asm = AssetDatabase.LoadAssetAtPath<CustomRecipeOptionalBurgerSO>(asset.assetPath);
            if (asm == null || asm == master)
                continue;
            ordered.Add(asm);
        }
        for (int i = 0; i < ordered.Count; i++)
            MergeLayerModelBindings(ordered[i], modelByGuid, modelGoByGuid);
    }

    private static void MergeLayerModelBindings(
        CustomRecipeOptionalBurgerSO asm,
        Dictionary<string, PseudoPrefabSO> modelByGuid,
        Dictionary<string, GameObject> modelGoByGuid)
    {
        var opts = asm.optionalSOs ?? new ScriptableObject[0];
        var modelSOs = asm.ingredientModelSOs ?? new PseudoPrefabSO[0];
        var models = asm.ingredientModels ?? new GameObject[0];
        for (int i = 0; i < opts.Length; i++)
        {
            if (opts[i] == null)
                continue;
            var op = AssetDatabase.GetAssetPath(opts[i]);
            if (string.IsNullOrEmpty(op))
                continue;
            var og = AssetDatabase.AssetPathToGUID(op);
            if (string.IsNullOrEmpty(og))
                continue;
            if (!modelByGuid.ContainsKey(og) && i < modelSOs.Length && modelSOs[i] != null)
                modelByGuid[og] = modelSOs[i];
            if (!modelGoByGuid.ContainsKey(og) && i < models.Length && models[i] != null)
                modelGoByGuid[og] = models[i];
        }
    }

    /// <summary>解析单层堆叠模型：本地已有绑定优先，缺失时继承 commonW2 组装定义的绑定，
    /// 再缺则回退该夹心菜谱自带的模型（CustomRecipeSO.model）；modelSO 优先于直接
    /// GameObject 引用（避免双写）。
    ///
    /// 第三级兜底（2026-09-15）：夹心本质就是一种自定义菜谱，用「新建菜谱 + 上传模型」
    /// 做出来的夹心已经有 so.model（&lt;菜谱目录&gt;/models/&lt;Id&gt;/&lt;Id&gt;.prefab，
    /// 见 LayoutEditorLevelAdminApi.UploadCustomRecipeModel）。没有这级兜底时，
    /// 自制夹心在汉堡里的叠层拿不到模型 → 运行时 OrderToPrefabLookup 取到 null →
    /// 该层完全不可见（即 SyncLevelBurgerOptionalFromBurgers 里那条告警的成因）。
    /// 宿主 RecipeHelper 不允许改动，所以兜底必须在编辑器侧落到 ingredientModels[i]。
    /// 注：so.model 是为「装盘」生成的三层 prefab（带 pivot/scale），当堆叠层用可能有
    /// 尺寸偏差 —— 但「有模型」远好于「不可见」，用户仍可在工作台手动改绑。</summary>
    private static void ResolveLayerModel(
        ScriptableObject layer,
        Dictionary<string, PseudoPrefabSO> localModelByGuid,
        Dictionary<string, GameObject> localGoByGuid,
        Dictionary<string, PseudoPrefabSO> w2ModelByGuid,
        Dictionary<string, GameObject> w2GoByGuid,
        out PseudoPrefabSO modelSO,
        out GameObject modelGo)
    {
        modelSO = null;
        modelGo = null;
        var lp = AssetDatabase.GetAssetPath(layer);
        if (string.IsNullOrEmpty(lp))
            return;
        var lg = AssetDatabase.AssetPathToGUID(lp);
        if (string.IsNullOrEmpty(lg))
            return;
        localModelByGuid.TryGetValue(lg, out modelSO);
        localGoByGuid.TryGetValue(lg, out modelGo);
        if (modelSO == null)
            w2ModelByGuid.TryGetValue(lg, out modelSO);
        if (modelGo == null && modelSO == null)
            w2GoByGuid.TryGetValue(lg, out modelGo);
        if (modelGo != null || modelSO != null)
            return;

        // 第三级：夹心菜谱自带模型（自制夹心的常规路径）
        var custom = layer as CustomRecipeSO;
        if (custom == null)
            return;
        if (custom.model != null)
        {
            modelGo = custom.model;
            return;
        }
        if (custom.modelSO != null)
            modelSO = custom.modelSO;
    }

    /// <summary>按已选成品汉堡夹心层全集（各食材最大重复数）写入本关 BurgerOptional.optionalSOs，
    /// 并把 bunSO 同步为所选汉堡的主面包（出现次数最多；平票取首个）。
    /// 覆盖语义：夹心/模型数组每次全量重建，模型绑定唯一来源 = commonW2 组装定义
    /// （本关旧绑定不保留，避免错位残渣）。返回 null = 正常，否则为告警文本。</summary>
    internal static string SyncLevelBurgerOptionalFromBurgers(
        CustomRecipeOptionalBurgerSO asm,
        IList<CustomRecipeSO> burgers)
    {
        if (asm == null || burgers == null || burgers.Count == 0)
            return null;

        var maxCount = new Dictionary<string, int>(StringComparer.Ordinal);
        var soByGuid = new Dictionary<string, ScriptableObject>(StringComparer.Ordinal);
        var order = new List<string>();
        var bunCount = new Dictionary<string, int>(StringComparer.Ordinal);
        var bunByGuid = new Dictionary<string, ScriptableObject>(StringComparer.Ordinal);
        var bunOrder = new List<string>();

        for (int b = 0; b < burgers.Count; b++)
        {
            var burger = burgers[b];
            if (burger == null || !IsFinishedBurgerRecipe(burger))
                continue;
            var perBurgerCount = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var c in burger.compositionSOs ?? new ScriptableObject[0])
            {
                if (c == null)
                    continue;
                var cp = AssetDatabase.GetAssetPath(c);
                if (string.IsNullOrEmpty(cp))
                    continue;
                var g = AssetDatabase.AssetPathToGUID(cp);
                if (string.IsNullOrEmpty(g))
                    continue;
                if (IsBurgerBunId(Path.GetFileNameWithoutExtension(cp)))
                {
                    if (!bunByGuid.ContainsKey(g))
                    {
                        bunByGuid[g] = c;
                        bunOrder.Add(g);
                        bunCount[g] = 0;
                    }
                    bunCount[g] = bunCount[g] + 1;
                    continue;
                }
                if (!soByGuid.ContainsKey(g))
                {
                    soByGuid[g] = c;
                    order.Add(g);
                    maxCount[g] = 0;
                }
                int cnt;
                perBurgerCount.TryGetValue(g, out cnt);
                perBurgerCount[g] = cnt + 1;
            }
            foreach (var kv in perBurgerCount)
            {
                if (maxCount[kv.Key] < kv.Value)
                    maxCount[kv.Key] = kv.Value;
            }
        }

        // bunSO 同步：主面包 = 出现次数最多（平票取首个）。模板拷贝残渣（DLC08/DLC02）
        // 会在这一步对齐到关卡实际使用的面包。
        string warning = null;
        PseudoPrefabSO mainBun = null;
        int maxBunCount = 0;
        for (int i = 0; i < bunOrder.Count; i++)
        {
            if (bunCount[bunOrder[i]] > maxBunCount)
            {
                maxBunCount = bunCount[bunOrder[i]];
                mainBun = bunByGuid[bunOrder[i]] as PseudoPrefabSO;
            }
        }
        if (bunOrder.Count > 1)
        {
            var names = new List<string>();
            for (int i = 0; i < bunOrder.Count; i++)
            {
                var np = AssetDatabase.GUIDToAssetPath(bunOrder[i]);
                names.Add(string.IsNullOrEmpty(np) ? bunOrder[i] : Path.GetFileNameWithoutExtension(np));
            }
            warning = "一关内汉堡使用了 " + bunOrder.Count + " 种面包（"
                + string.Join("、", names.ToArray())
                + "），BurgerOptional 仅绑定主面包 " + (mainBun != null ? mainBun.name : "?")
                + "——其余面包沿用官方叠层规则。建议统一改用「🍔 Burger大全」（commonW2）的汉堡，"
                + "它们的面皮一律是 DLC8 面皮 dlc08_choppedbun；官方 core 汉堡用的是 DLC02 面皮，"
                + "与之混选就会触发本告警";
        }

        var opts = new List<ScriptableObject>();
        for (int i = 0; i < order.Count; i++)
        {
            var g = order[i];
            var n = maxCount[g];
            var so = soByGuid[g];
            for (int j = 0; j < n; j++)
                opts.Add(so);
        }

        // 模型绑定直接覆盖：不保留本关旧绑定（旧数据可能携带错位/大小写错误的残渣），
        // 一律以 commonW2 组装定义的规范绑定为唯一来源；要改绑定请改 commonW2 母本
        // （OptionalBurger.asset 等），所有关卡重新填充即生效。
        Dictionary<string, PseudoPrefabSO> w2ModelByGuid;
        Dictionary<string, GameObject> w2GoByGuid;
        CollectCommonW2LayerModelBindings(out w2ModelByGuid, out w2GoByGuid);
        var modelByGuid = new Dictionary<string, PseudoPrefabSO>(StringComparer.Ordinal);
        var goByGuid = new Dictionary<string, GameObject>(StringComparer.Ordinal);

        var newModelSOs = new List<PseudoPrefabSO>();
        var newModels = new List<GameObject>();
        var unboundCustoms = new List<string>();
        for (int i = 0; i < opts.Count; i++)
        {
            PseudoPrefabSO mso;
            GameObject mgo;
            ResolveLayerModel(opts[i], modelByGuid, goByGuid, w2ModelByGuid, w2GoByGuid, out mso, out mgo);
            newModelSOs.Add(mso);
            newModels.Add(mgo);
            // 原生食材的 None 属设计内（运行时回退官方面包 oldLookup 取模型）；
            // 自定义中间产物不在官方表内，双 None = 叠层隐形，必须提示。
            if (mso == null && mgo == null && opts[i] is CustomRecipeSO)
            {
                var name = ((CustomRecipeSO)opts[i]).recipeName;
                if (string.IsNullOrEmpty(name))
                    name = opts[i].name;
                if (!unboundCustoms.Contains(name))
                    unboundCustoms.Add(name);
            }
        }
        if (unboundCustoms.Count > 0)
            LayoutEditorLog.LogWarning("[Optional] BurgerOptional 以下中间产物夹心无堆叠模型"
                + "（commonW2 绑定、官方面包表、菜谱自带 model/modelSO 三处均为空，叠层将不可见）："
                + string.Join("、", unboundCustoms.ToArray())
                + "。请在汉堡工作台给它上传/生成一个模型，或手动绑定堆叠模型。");

        Undo.RecordObject(asm, "Sync Burger Assembly From Selected");
        asm.optionalSOs = opts.ToArray();
        asm.ingredientModelSOs = newModelSOs.ToArray();
        asm.ingredientModels = newModels.ToArray();
        if (mainBun != null)
            asm.bunSO = mainBun;
        if (opts.Count > asm.ingredientContainerCapacity)
            asm.ingredientContainerCapacity = opts.Count;
        EditorUtility.SetDirty(asm);
        AssetDatabase.SaveAssets();
        LayoutEditorLog.Log("[Optional] 已同步本关 BurgerOptional optionalSOs（" + opts.Count + " 层，面包 "
            + (asm.bunSO != null ? asm.bunSO.name : "无") + "）");
        return warning;
    }

    /// <summary>GET /api/level/burger-optional：返回本关 BurgerOptional 组装定义。</summary>
    public static BurgerDefinitionDto GetLevelBurgerOptionalDefinition(string levelInfoAssetPath)
    {
        var optional = FindLevelBurgerOptional(levelInfoAssetPath);
        if (optional == null)
            return null;
        var path = LevelBurgerOptionalAssetPath(levelInfoAssetPath);
        var guid = AssetDatabase.AssetPathToGUID(path);
        var zhMap = new Dictionary<string, string>(StringComparer.Ordinal);
        zhMap[LevelBurgerOptionalFileName] = "本关汉堡夹心";
        return BuildDefinitionDto(optional, path, guid, zhMap);
    }

    /// <summary>commonW2 参考组装定义（创建本关 BurgerOptional 的壳模板）。
    /// 优先 OptionalBurger 主模板；bunSO 只是占位（SyncLevelBurgerOptionalFromBurgers
    /// 会按关卡实际面包覆盖），不要再默认 ChickenBurgerAssembly（其面包是 DLC08）。
    /// 查找走 FindCommonW2Assembly 递归扫描（细分类迁移后模板位于 burger/assembly/）。</summary>
    private static CustomRecipeOptionalBurgerSO FindCommonW2BurgerModelAssembly()
    {
        var master = FindCommonW2Assembly("OptionalBurger");
        if (master != null)
            return master;
        return FindCommonW2Assembly("ChickenBurgerAssembly");
    }

    /// <summary>面包皮候选池（恒为 ChoppedBunSO / DLC02_ChoppedBun / dlc08_choppedbun 三个）。
    ///  internal：「一键统一面包皮」（LayoutEditorBunSwapApi）复用同一份候选与 bundle 可用性判定。</summary>
    internal static BurgerCandidateDto[] CollectBuns()
    {
        var list = new List<BurgerCandidateDto>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var folder in LayoutEditorLevelAdminApi.PseudoPrefabSearchFolders)
        {
            if (folder.IndexOf("/Ingredients", StringComparison.Ordinal) < 0)
                continue;
            if (!LayoutEditorLevelAdminApi.AssetFolderExists(folder))
                continue;
            foreach (var asset in LayoutEditorLevelAdminApi.ScanAssetsByScript(
                         folder, LayoutEditorLevelAdminApi.PseudoPrefabScriptGuid))
            {
                var id = Path.GetFileNameWithoutExtension(asset.assetPath);
                if (!IsBurgerBunId(id) || !seen.Add(asset.guid))
                    continue;
                var bunSo = AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(asset.assetPath);
                string zh, en;
                LayoutEditorManualLookup.TryGet(id, out zh, out en);
                // ⚠ 必须显式填 bundleAvailable：DTO 里它是 bool，默认 false 会被前端判成
                //   「bundle 缺失」直接禁用 —— 面包皮全灭。同理补 modelState/iconState，
                //   让面包与其它候选走同一套徽标/图标口径。
                var bunBundle = bunSo != null ? (bunSo.bundleName ?? "") : "";
                list.Add(new BurgerCandidateDto
                {
                    guid = asset.guid,
                    id = id,
                    nameZh = string.IsNullOrEmpty(zh) ? id : zh,
                    nameEn = string.IsNullOrEmpty(en) ? id : en,
                    kind = "bun",
                    assetPath = asset.assetPath,
                    modelState = "bound",   // 面包是底座，模型由官方 cosmetic 决定，不需要堆叠模型
                    iconState = "none",     // 走 /icons/ingredients/<id>.png
                    iconFallbackIds = new string[0],
                    matchlistKey = LayoutEditorMatchlistMap.MatchlistKeyOfAsset(bunSo) ?? "",
                    bundleName = bunBundle,
                    bundleAvailable = string.IsNullOrEmpty(bunBundle)
                        || LayoutEditorCatalogApi.BundleFileExists(bunBundle),
                    recommended = true,
                    score = 0,
                    previewable = false,
                });
            }
        }
        list.Sort((a, b) => string.Compare(a.nameZh, b.nameZh, StringComparison.Ordinal));
        return list.ToArray();
    }

    /// <summary>已有成品汉堡：commonW2 共享库（模板）+ 指定关卡集本地产出（Composite 自定义菜谱）。</summary>
    private static BurgerProductDto[] CollectProducts(string setName)
    {
        var list = new List<BurgerProductDto>();
        CollectProductsFrom(LayoutEditorLevelAdminApi.CommonW2RecipesDir, list);
        if (!string.IsNullOrEmpty(setName))
            CollectProductsFrom(LayoutEditorLevelAdminApi.LevelSetsRoot + "/" + setName + "/custom_recipes", list);
        list.Sort((a, b) => string.Compare(a.nameZh, b.nameZh, StringComparison.Ordinal));
        return list.ToArray();
    }

    private static void CollectProductsFrom(string dir, List<BurgerProductDto> list)
    {
        if (!LayoutEditorLevelAdminApi.AssetFolderExists(dir))
            return;
        var zhMap = LayoutEditorLevelAdminApi.LoadCustomRecipeZhMap(dir);
        foreach (var asset in LayoutEditorLevelAdminApi.ScanAssetsByScript(
                     dir, LayoutEditorLevelAdminApi.CustomRecipeScriptGuid))
        {
            var so = AssetDatabase.LoadAssetAtPath<CustomRecipeSO>(asset.assetPath);
            if (so == null || so.score <= 0 || so.type != CustomRecipeSO.RecipeType.Composite)
                continue;
            var comps = new List<string>();
            foreach (var c in so.compositionSOs ?? new ScriptableObject[0])
            {
                if (c == null) continue;
                var cp = AssetDatabase.GetAssetPath(c);
                if (!string.IsNullOrEmpty(cp))
                    comps.Add(Path.GetFileNameWithoutExtension(cp));
            }
            var key = !string.IsNullOrEmpty(so.recipeName) ? so.recipeName : Path.GetFileNameWithoutExtension(asset.assetPath);
            var fallbackId = Path.GetFileNameWithoutExtension(asset.assetPath);
            string zh;
            if (!zhMap.TryGetValue(key, out zh) || string.IsNullOrEmpty(zh))
                zh = fallbackId;
            // 英文名：names.json 里与中文名同源，载入已有汉堡时要能回填到表单
            string tmpZh, tmpEn;
            var en = key;
            if (LayoutEditorLevelAdminApi.TryGetCustomRecipeDisplayName(dir, key, out tmpZh, out tmpEn)
                || LayoutEditorLevelAdminApi.TryGetCustomRecipeDisplayName(dir, fallbackId, out tmpZh, out tmpEn))
            {
                if (!string.IsNullOrEmpty(tmpEn))
                    en = tmpEn;
            }
            list.Add(new BurgerProductDto
            {
                guid = asset.guid,
                id = fallbackId,
                assetPath = asset.assetPath,
                recipeName = key,
                nameZh = zh,
                nameEn = en,
                score = so.score,
                compositionIds = comps.ToArray(),
            });
        }
    }

    /// <summary>堆叠模型来源（两路合并）：
    ///  1. local —— commonW2 models 目录里已有的 PseudoPrefabSO 包装（14 个，可直接绑定）；
    ///  2. pool  —— layout-editor/scripts/data/burger-model-pool.json（dump_bundle 提取的
    ///     524 个原版 plated/prep/recipe prefab），选用时才按需生成包装 SO。
    ///  池文件缺失时自动降级为仅 local，并给一次告警。</summary>
    private static BurgerModelDto[] CollectModels()
    {
        var list = new List<BurgerModelDto>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        var modelsDir = CommonW2BurgerDir() + "/models";
        if (LayoutEditorLevelAdminApi.AssetFolderExists(modelsDir))
        {
            foreach (var asset in LayoutEditorLevelAdminApi.ScanAssetsByScript(
                         modelsDir, LayoutEditorLevelAdminApi.PseudoPrefabScriptGuid))
            {
                var so = AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(asset.assetPath);
                if (so == null)
                    continue;
                var id = Path.GetFileNameWithoutExtension(asset.assetPath);
                if (!seen.Add(id))
                    continue;
                list.Add(new BurgerModelDto
                {
                    id = id,
                    assetPath = asset.assetPath,
                    prefabName = so.prefabName ?? "",
                    bundleName = so.bundleName ?? "",
                    source = "local",
                });
            }
        }

        foreach (var entry in LoadModelPool())
        {
            if (!seen.Add(entry.id))
                continue;
            list.Add(new BurgerModelDto
            {
                id = entry.id,
                assetPath = "",              // 尚未生成包装 SO
                prefabName = entry.id,
                bundleName = entry.bundle,
                source = "pool",
                bundleAssetPath = entry.assetPath,
                dlc = entry.dlc,
                stackable = entry.stackable,
            });
        }

        list.Sort(delegate (BurgerModelDto a, BurgerModelDto b)
        {
            if (a.source != b.source)
                return a.source == "local" ? -1 : 1;
            return string.Compare(a.id, b.id, StringComparison.Ordinal);
        });
        return list.ToArray();
    }

    [Serializable]
    private class ModelPoolEntry
    {
        public string id;
        public string bundle;
        public string assetPath;
        public string kind;
        public string dlc;
        public string norm;
        public bool stackable;
    }

    [Serializable]
    private class ModelPoolFile
    {
        public int count;
        public ModelPoolEntry[] items;
    }

    private static ModelPoolEntry[] _modelPool;
    private static bool _modelPoolWarned;

    /// <summary>读取 burger-model-pool.json（由 gen-burger-model-pool.mjs 生成，已入库；
    ///  编辑器**不依赖** 7.7GB 的 dump_bundle 本体）。缺失时返回空并告警一次。</summary>
    private static ModelPoolEntry[] LoadModelPool()
    {
        if (_modelPool != null)
            return _modelPool;
        var path = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../layout-editor/scripts/data/burger-model-pool.json"));
        if (!File.Exists(path))
        {
            if (!_modelPoolWarned)
            {
                _modelPoolWarned = true;
                LayoutEditorLog.LogWarning("[Burger] 未找到原版模型池 burger-model-pool.json，"
                    + "堆叠模型只能从 commonW2/models 里选。生成方式："
                    + "node layout-editor/scripts/gen-burger-model-pool.mjs");
            }
            _modelPool = new ModelPoolEntry[0];
            return _modelPool;
        }
        try
        {
            var file = JsonUtility.FromJson<ModelPoolFile>(File.ReadAllText(path));
            _modelPool = file != null && file.items != null ? file.items : new ModelPoolEntry[0];
        }
        catch (Exception ex)
        {
            LayoutEditorLog.LogWarning("[Burger] 原版模型池解析失败：" + ex.Message);
            _modelPool = new ModelPoolEntry[0];
        }
        return _modelPool;
    }

    /// <summary>把池条目落成 commonW2/models 下的 PseudoPrefabSO 包装（选用时才生成）。
    ///  assetPath 逐字符照抄池里的 container —— bundle 内实名大小写敏感，写错运行时取不到。</summary>
    private static PseudoPrefabSO MaterializePoolModel(string id)
    {
        var pool = LoadModelPool();
        ModelPoolEntry entry = null;
        for (int i = 0; i < pool.Length; i++)
        {
            if (string.Equals(pool[i].id, id, StringComparison.Ordinal))
            {
                entry = pool[i];
                break;
            }
        }
        if (entry == null)
            return null;
        if (!LayoutEditorCatalogApi.BundleFileExists(entry.bundle))
        {
            LayoutEditorLog.LogWarning("[Burger] 模型 " + id + " 所在 bundle " + entry.bundle
                + " 不在 StreamingAssets，拒绝生成包装。");
            return null;
        }
        var modelsDir = CommonW2BurgerDir() + "/models";
        if (!LayoutEditorLevelAdminApi.AssetFolderExists(modelsDir))
            return null;
        var assetPath = modelsDir + "/" + id + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(assetPath);
        if (existing != null)
            return existing;

        var so = ScriptableObject.CreateInstance<PseudoPrefabSO>();
        so.prefabName = entry.id;
        so.bundleName = entry.bundle;
        so.assetPath = entry.assetPath;
        AssetDatabase.CreateAsset(so, assetPath);
        EditorUtility.SetDirty(so);
        AssetDatabase.SaveAssets();
        LayoutEditorLog.Log("[Burger] 已生成原版模型包装：" + assetPath
            + "（" + entry.bundle + " / " + entry.assetPath + "）");
        return AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(assetPath);
    }

    /// <summary>夹心候选池（2026-09-15 放开）。
    ///
    /// 旧版只给「commonW2 各组装定义 optionalSOs 的并集」（约 20 项）并硬拒其余，
    /// 导致用官方食材/官方菜谱/自制中间产物做夹心一律被挡。现在给出全量候选：
    ///   - 官方食材        PseudoPrefabSearchFolders 的 Ingredients + commonW1 food（含 30 项盲区）
    ///   - 官方菜谱节点    OfficialRecipeSearchFolders + commonW2/food/Recipes
    ///   - 自定义菜谱      commonW2 汉堡大全 + common01 官方自定义 + 当前关卡集
    /// 每条附带 modelState / matchlistKey / bundleAvailable，让前端能分组、排序、给出告警。
    /// 真正的拦截只剩「bundle 不在 StreamingAssets」（运行时会崩），见 ValidateFillerLayers。
    /// </summary>
    private static BurgerCandidateDto[] CollectCandidates(string setName, bool includeModelless)
    {
        var list = new List<BurgerCandidateDto>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var recommended = CollectCommonW2BurgerFillerGuids();

        Dictionary<string, PseudoPrefabSO> w2ModelByGuid;
        Dictionary<string, GameObject> w2GoByGuid;
        CollectCommonW2LayerModelBindings(out w2ModelByGuid, out w2GoByGuid);

        var w2Dir = LayoutEditorLevelAdminApi.CommonW2RecipesDir;

        // 1) 官方食材（PseudoPrefabSO）
        foreach (var folder in LayoutEditorLevelAdminApi.PseudoPrefabSearchFolders)
        {
            // 烹饪步骤/装盘容器不是夹心
            if (folder.IndexOf("/CookingSteps", StringComparison.Ordinal) >= 0 ||
                folder.IndexOf("/PlatingSteps", StringComparison.Ordinal) >= 0)
                continue;
            if (!LayoutEditorLevelAdminApi.AssetFolderExists(folder))
                continue;
            foreach (var asset in LayoutEditorLevelAdminApi.ScanAssetsByScript(
                         folder, LayoutEditorLevelAdminApi.PseudoPrefabScriptGuid))
            {
                var id = Path.GetFileNameWithoutExtension(asset.assetPath);
                if (IsBurgerBunId(id))
                    continue; // 面包单列（CollectBuns）
                var so = AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(asset.assetPath);
                if (so == null)
                    continue;
                AddFillerCandidate(list, seen, asset.guid, asset.assetPath, so, "ingredient",
                    w2Dir, recommended, w2ModelByGuid, w2GoByGuid);
            }
        }

        // 2) 官方菜谱节点（PseudoPrefabSORecipe）
        //    官方成品菜一律不再作为夹心候选 —— 汤/套餐/寿司/披萨/火锅/炸物/甜甜圈……
        //    都是「一整道菜」，夹进汉堡没有意义。
        //    唯一例外：**Burger大全组装定义已经在用的**（recommended）。目前是 2 条
        //    DLC05 早餐拼盘 Breakfast_Bacon_Egg / Breakfast_Bacon_Egg_Sausage，
        //    它们是 4 个早餐汉堡的实际夹心层；若一并剔除，这些汉堡将无法再被创建或改层。
        //    这些条目会落在前端的「⭐ 常用夹心」组，不会单独成组。
        var recipeFolders = new List<string>(LayoutEditorLevelAdminApi.OfficialRecipeSearchFolders);
        recipeFolders.Add("Assets/commonW2/food/Recipes");
        foreach (var folder in recipeFolders)
        {
            if (!LayoutEditorLevelAdminApi.AssetFolderExists(folder))
                continue;
            foreach (var asset in LayoutEditorLevelAdminApi.ScanAssetsByScript(
                         folder, LayoutEditorLevelAdminApi.OriginalRecipeScriptGuid))
            {
                var so = AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(asset.assetPath);
                if (so == null)
                    continue;
                if (!includeModelless && !recommended.Contains(asset.guid))
                    continue;
                AddFillerCandidate(list, seen, asset.guid, asset.assetPath, so, "official-recipe",
                    w2Dir, recommended, w2ModelByGuid, w2GoByGuid);
            }
        }

        // 3) 自定义菜谱（只收中间产物；排除组装定义自身）
        //    三条硬门槛（2026-09-15 收口）：
        //     a. 成品汉堡不能当夹心 —— 汉堡夹汉堡无意义，且会把 47 条成品汉堡灌进候选列表；
        //        「载入已有汉堡」走 CollectProducts，是另一条链路，不受影响。
        //     b. **成品菜（score > 0）不能当夹心** —— 与官方成品菜同一口径：一碗汤、一个披萨、
        //        一块月饼都是「一整道菜」，夹进汉堡没有意义（实测共享库里就有 4 汤 + 1 披萨）。
        //        夹心按定义是 0 分中间产物，夹心工作台也默认预设 score=0。
        //     c. 无堆叠模型（commonW2 绑定 / 自带 model / modelSO 三处皆空）一律不收 ——
        //        选了运行时也看不见（OrderToPrefabLookup 取到 null）。典型是 Mixed* 搅拌糊：
        //        它们本就该先下锅变成 Panfried* 再当夹心。
        //    b、c 的例外都是「commonW2 组装定义已在用」（recommended），避免把现有汉堡的
        //    实际夹心层挡在门外导致无法改层；前端「显示全部候选」开关（includeModelless）
        //    可放行全部，用于排查。
        var customFolders = new List<string>();
        if (LayoutEditorLevelAdminApi.AssetFolderExists(w2Dir))
            customFolders.Add(w2Dir);
        const string commonCustom = "Assets/common01/food/CustomRecipes";
        if (LayoutEditorLevelAdminApi.AssetFolderExists(commonCustom))
            customFolders.Add(commonCustom);
        if (!string.IsNullOrEmpty(setName))
        {
            var setDir = LayoutEditorLevelAdminApi.LevelSetsRoot + "/" + setName + "/custom_recipes";
            if (LayoutEditorLevelAdminApi.AssetFolderExists(setDir))
                customFolders.Add(setDir);
        }
        foreach (var folder in customFolders)
        {
            foreach (var asset in LayoutEditorLevelAdminApi.ScanAssetsByScript(
                         folder, LayoutEditorLevelAdminApi.CustomRecipeScriptGuid))
            {
                var so = AssetDatabase.LoadAssetAtPath<CustomRecipeSO>(asset.assetPath);
                if (so == null || so is CustomRecipeOptionalBurgerSO || so is CustomRecipeOptionalPizzaSO)
                    continue;
                // a. 成品汉堡不做夹心
                if (IsFinishedBurgerRecipe(so))
                    continue;
                // b. 成品菜（score>0）不做夹心；commonW2 已在用的除外
                if (!includeModelless && so.score > 0 && !recommended.Contains(asset.guid))
                    continue;
                // c. 统一「有模型」门槛
                if (!includeModelless
                    && ModelStateOf(asset.guid, so, w2ModelByGuid, w2GoByGuid) == "none")
                    continue;
                AddFillerCandidate(list, seen, asset.guid, asset.assetPath, so, "custom",
                    folder == w2Dir || folder == commonCustom ? w2Dir : folder,
                    recommended, w2ModelByGuid, w2GoByGuid);
            }
        }

        // 推荐项置顶，其余按中文名
        list.Sort(delegate (BurgerCandidateDto a, BurgerCandidateDto b)
        {
            if (a.recommended != b.recommended)
                return a.recommended ? -1 : 1;
            return string.Compare(a.nameZh, b.nameZh, StringComparison.Ordinal);
        });
        return list.ToArray();
    }

    /// <summary>commonW2/custom_recipes/burger 下全部组装定义 optionalSOs 的去重 guid 集合
    ///  （递归含 assembly/ 等子分类目录）。</summary>
    internal static HashSet<string> CollectCommonW2BurgerFillerGuids()
    {
        var guids = new HashSet<string>(StringComparer.Ordinal);
        var burgerDir = CommonW2BurgerDir();
        if (!LayoutEditorLevelAdminApi.AssetFolderExists(burgerDir))
            return guids;
        foreach (var asset in LayoutEditorLevelAdminApi.ScanAssetsByScript(
                     burgerDir, LayoutEditorLevelAdminApi.OptionalBurgerScriptGuid))
        {
            var asm = AssetDatabase.LoadAssetAtPath<CustomRecipeOptionalBurgerSO>(asset.assetPath);
            if (asm == null)
                continue;
            foreach (var opt in asm.optionalSOs ?? new ScriptableObject[0])
            {
                if (opt == null)
                    continue;
                var op = AssetDatabase.GetAssetPath(opt);
                if (string.IsNullOrEmpty(op))
                    continue;
                var g = AssetDatabase.AssetPathToGUID(op);
                if (!string.IsNullOrEmpty(g))
                    guids.Add(g);
            }
        }
        return guids;
    }

    /// <summary>堆叠模型状态，与 ResolveLayerModel 的三级回退口径完全一致：
    ///  bound = commonW2 组装定义里已有绑定；own = 菜谱自带 model/modelSO；none = 三处皆空。
    ///  官方食材（非 CustomRecipeSO）的 none 属设计内（运行时回退官方面包 lookup），
    ///  故只对自定义菜谱做门槛判定。</summary>
    private static string ModelStateOf(
        string guid,
        CustomRecipeSO crs,
        Dictionary<string, PseudoPrefabSO> w2ModelByGuid,
        Dictionary<string, GameObject> w2GoByGuid)
    {
        if (!string.IsNullOrEmpty(guid)
            && (w2ModelByGuid.ContainsKey(guid) || w2GoByGuid.ContainsKey(guid)))
            return "bound";
        if (crs != null && (crs.model != null || crs.modelSO != null))
            return "own";
        return "none";
    }

    /// <summary>共享占位图标判定：文件名含 "Generic" 的图标是「一图多用」的占位图，
    ///  不能代表这道菜。commonW2 的 FriedGeneric.png（实为一整颗洋葱）被 26 个资产共用，
    ///  直接下发会让 24 个夹心在工作台里全是洋葱，必须降级到食材图标回退。
    ///  库内其余图标最多被 4 个资产共用，不会误伤。</summary>
    private static bool IsGenericIconName(string iconAssetPath)
    {
        if (string.IsNullOrEmpty(iconAssetPath))
            return false;
        return Path.GetFileNameWithoutExtension(iconAssetPath)
            .IndexOf("Generic", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void AddFillerCandidate(
        List<BurgerCandidateDto> list,
        HashSet<string> seen,
        string guid,
        string path,
        ScriptableObject so,
        string kind,
        string namesDir,
        HashSet<string> recommendedGuids,
        Dictionary<string, PseudoPrefabSO> w2ModelByGuid,
        Dictionary<string, GameObject> w2GoByGuid)
    {
        if (!seen.Add(guid))
            return;
        var id = Path.GetFileNameWithoutExtension(path);
        string zh = id;
        string en = id;
        var crs = so as CustomRecipeSO;
        if (kind == "custom")
        {
            var key = crs != null && !string.IsNullOrEmpty(crs.recipeName) ? crs.recipeName : id;
            string tmpZh, tmpEn;
            if (LayoutEditorLevelAdminApi.TryGetCustomRecipeDisplayName(namesDir, key, out tmpZh, out tmpEn)
                || LayoutEditorLevelAdminApi.TryGetCustomRecipeDisplayName(namesDir, id, out tmpZh, out tmpEn))
            {
                zh = tmpZh;
                en = tmpEn;
            }
            else if (!LayoutEditorManualLookup.TryGet(key, out zh, out en))
                LayoutEditorManualLookup.TryGet(id, out zh, out en);
        }
        else
        {
            LayoutEditorManualLookup.TryGet(id, out zh, out en);
        }
        if (string.IsNullOrEmpty(zh))
            zh = id;
        if (string.IsNullOrEmpty(en))
            en = id;

        var modelState = ModelStateOf(guid, crs, w2ModelByGuid, w2GoByGuid);

        // 图标状态 + 食材回退链（自定义菜谱没有专属图标时，用叶食材图标区分）
        var iconState = "none";
        string[] iconFallbackIds = new string[0];
        if (crs != null)
        {
            if (crs.icon != null)
            {
                var iconPath = AssetDatabase.GetAssetPath(crs.icon);
                iconState = IsGenericIconName(iconPath) ? "generic" : "own";
            }
            var leaves = LayoutEditorRecipeKnowledge.CustomIngredients(crs);
            if (leaves != null && leaves.Count > 0)
                iconFallbackIds = leaves.ToArray();
        }

        var pseudo = so as PseudoPrefabSO;
        var bundleName = pseudo != null ? (pseudo.bundleName ?? "") : "";
        var matchlistKey = LayoutEditorMatchlistMap.MatchlistKeyOfAsset(so) ?? "";

        list.Add(new BurgerCandidateDto
        {
            guid = guid,
            id = id,
            nameZh = zh,
            nameEn = en,
            kind = kind,
            assetPath = path,
            modelState = modelState,
            iconState = iconState,
            iconFallbackIds = iconFallbackIds,
            matchlistKey = matchlistKey,
            bundleName = bundleName,
            // 自定义菜谱不指向游戏 bundle，恒为可用
            bundleAvailable = string.IsNullOrEmpty(bundleName)
                || LayoutEditorCatalogApi.BundleFileExists(bundleName),
            recommended = recommendedGuids != null && recommendedGuids.Contains(guid),
            score = crs != null ? crs.score : 0,
            previewable = crs != null && HasLocalModelFiles(path),
        });
    }

    /// <summary>该菜谱的 models/&lt;Id&gt;/ 目录里有没有可供网页 3D 预览的本地网格。
    ///  只有走「上传模型 / 模板网格」生成过模型的自定义菜谱才有；指向游戏 bundle 的
    ///  PseudoPrefabSO 模型没有本地文件，网页无法预览。</summary>
    internal static bool HasLocalModelFiles(string recipeAssetPath)
    {
        var files = LayoutEditorLevelAdminApi.ListCustomRecipeModelFiles(recipeAssetPath);
        for (int i = 0; i < files.Length; i++)
        {
            var ext = Path.GetExtension(files[i]).ToLowerInvariant();
            if (ext == ".fbx" || ext == ".obj")
                return true;
        }
        return false;
    }

    /// <summary>夹心层校验（2026-09-15 由「白名单硬拒」改为「只拦真正会崩的，其余给告警」）。
    ///
    ///  硬拒条件只剩一个：PseudoPrefabSO 指向的 bundle 不在 StreamingAssets ——
    ///  运行时 PseudoPrefabManager.GetAssetBundle 会抛 KeyNotFoundException，必须挡住。
    ///
    ///  告警（不阻断，经 out warnings 回给前端）：
    ///   - 自定义菜谱没有任何堆叠模型（commonW2 绑定 / 自带 model / modelSO 三处皆空）
    ///     → 运行时 OrderToPrefabLookup 取到 null，该层不可见；
    ///   - 引用了官方 DLC 资产 → 需要对应 DLC 匹配表（保存菜谱时会自动补，仅提示）。
    ///  返回 null = 放行。</summary>
    private static string ValidateFillerLayers(IList<ScriptableObject> layers, out string[] warnings)
    {
        warnings = new string[0];
        Dictionary<string, PseudoPrefabSO> w2ModelByGuid;
        Dictionary<string, GameObject> w2GoByGuid;
        CollectCommonW2LayerModelBindings(out w2ModelByGuid, out w2GoByGuid);

        var blocked = new List<string>();
        var noModel = new List<string>();
        var dlcKeys = new List<string>();

        foreach (var layer in layers)
        {
            if (layer == null)
                continue;
            var lp = AssetDatabase.GetAssetPath(layer);
            var lid = string.IsNullOrEmpty(lp) ? layer.name : Path.GetFileNameWithoutExtension(lp);
            if (IsBurgerBunId(lid))
                continue;
            var lg = string.IsNullOrEmpty(lp) ? "" : AssetDatabase.AssetPathToGUID(lp);

            var pseudo = layer as PseudoPrefabSO;
            if (pseudo != null && !string.IsNullOrEmpty(pseudo.bundleName)
                && !LayoutEditorCatalogApi.BundleFileExists(pseudo.bundleName))
            {
                blocked.Add(lid + "（bundle " + pseudo.bundleName + " 未构建）");
                continue;
            }

            var key = LayoutEditorMatchlistMap.MatchlistKeyOfAsset(layer);
            if (!string.IsNullOrEmpty(key) && !dlcKeys.Contains(key))
                dlcKeys.Add(key);

            var crs = layer as CustomRecipeSO;
            if (crs == null)
                continue; // 官方生食材无模型属设计内：运行时回退官方面包 lookup
            var hasBinding = !string.IsNullOrEmpty(lg)
                && (w2ModelByGuid.ContainsKey(lg) || w2GoByGuid.ContainsKey(lg));
            if (!hasBinding && crs.model == null && crs.modelSO == null)
                noModel.Add(lid);
        }

        if (blocked.Count > 0)
            return "以下夹心指向的 bundle 不在 StreamingAssets，启用会导致运行时崩溃："
                + string.Join("、", blocked.ToArray());

        var warn = new List<string>();
        if (noModel.Count > 0)
            warn.Add("以下夹心没有堆叠模型，游戏里该层不可见："
                + string.Join("、", noModel.ToArray())
                + "。可在菜谱编辑里给它上传模型，或用「模板网格 + 贴图」一键生成。");
        if (dlcKeys.Count > 0)
        {
            dlcKeys.Sort(StringComparer.Ordinal);
            warn.Add("用到了 DLC 资产（" + string.Join("、", dlcKeys.ToArray())
                + "），保存菜谱时会自动补入对应 DLC 匹配表。");
        }
        warnings = warn.ToArray();
        return null;
    }

    private static BurgerDefinitionDto BuildDefinitionDto(
        CustomRecipeOptionalBurgerSO so, string assetPath, string guid,
        Dictionary<string, string> zhMap)
    {
        var layers = new List<BurgerLayerDto>();
        var opts = so.optionalSOs ?? new ScriptableObject[0];
        var modelSOs = so.ingredientModelSOs ?? new PseudoPrefabSO[0];
        var models = so.ingredientModels ?? new GameObject[0];
        for (int i = 0; i < opts.Length; i++)
        {
            var o = opts[i];
            if (o == null)
                continue;
            var p = AssetDatabase.GetAssetPath(o);
            var id = string.IsNullOrEmpty(p) ? "?" : Path.GetFileNameWithoutExtension(p);
            var g = string.IsNullOrEmpty(p) ? "" : AssetDatabase.AssetPathToGUID(p);
            var nameKey = o.name;
            var custom = o as CustomRecipeSO;
            if (custom != null && !string.IsNullOrEmpty(custom.recipeName))
                nameKey = custom.recipeName;
            string zh;
            if (custom == null || !zhMap.TryGetValue(nameKey, out zh) || string.IsNullOrEmpty(zh))
                zh = id;

            var mso = i < modelSOs.Length ? modelSOs[i] : null;
            var mp = mso != null ? AssetDatabase.GetAssetPath(mso) : null;
            layers.Add(new BurgerLayerDto
            {
                guid = g,
                id = id,
                nameZh = zh,
                kind = custom != null ? "custom"
                    : o is PseudoPrefabSORecipe ? "official-recipe" : "ingredient",
                modelId = !string.IsNullOrEmpty(mp) ? Path.GetFileNameWithoutExtension(mp) : "",
                modelAssetPath = mp ?? "",
                hasDirectModel = i < models.Length && models[i] != null,
            });
        }

        string nameZh;
        var recipeKey = !string.IsNullOrEmpty(so.recipeName) ? so.recipeName : Path.GetFileNameWithoutExtension(assetPath);
        if (!zhMap.TryGetValue(recipeKey, out nameZh) || string.IsNullOrEmpty(nameZh))
            nameZh = Path.GetFileNameWithoutExtension(assetPath);

        return new BurgerDefinitionDto
        {
            assetPath = assetPath,
            guid = guid,
            id = Path.GetFileNameWithoutExtension(assetPath),
            nameZh = nameZh,
            bunId = so.bunSO != null ? Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(so.bunSO) ?? "") : "",
            capacity = so.ingredientContainerCapacity,
            layers = layers.ToArray(),
        };
    }

    // ------------------------------------------------------------ 创建成品汉堡

    /// <summary>POST /api/burger/create：在<strong>当前关卡集</strong>生成/更新成品汉堡菜谱
    ///  （面包 + 夹心层，普通 CustomRecipeSO）。本关夹心 optional 由菜谱管理一键填充维护。</summary>
    public static string CreateBurger(BurgerCreateDto dto, out BurgerCreateResultDto result)
    {
        result = new BurgerCreateResultDto { ok = false, addedToDefinition = new string[0], warnings = new string[0] };
        if (dto == null || string.IsNullOrEmpty(dto.setName))
            return "缺少目标关卡集。";
        var setName = LayoutEditorLevelAdminApi.SanitizeName(dto.setName);
        var setDir = LayoutEditorLevelAdminApi.LevelSetsRoot + "/" + setName;
        if (!LayoutEditorLevelAdminApi.AssetFolderExists(setDir))
            return "关卡集不存在：" + dto.setName;
        if (string.IsNullOrEmpty(dto.definitionAssetPath))
            return "缺少参考组装定义。";
        var recipeName = SanitizeId(dto.recipeName);
        if (string.IsNullOrEmpty(recipeName))
            return "菜谱标识只能包含字母数字和下划线。";
        if (dto.layerIds == null || dto.layerIds.Length == 0)
            return "至少添加一层（含汉堡面包）。";

        var category = LayoutEditorLevelAdminApi.SanitizeName(dto.category ?? "");
        if (string.IsNullOrEmpty(category))
            category = "burger_local";
        if (string.Equals(category, LayoutEditorLevelAdminApi.BurgerCategoryId, StringComparison.Ordinal))
            return "「burger」为 Burger大全共享库保留分类 id，请换一个（如 burger_local）。";

        // 关卡集 custom_recipes 与分类目录（首次使用时初始化，与 CreateCustomRecipe 同规则）
        string recipesDir;
        string categoryDir;
        CustomRecipeConfigSO config;
        var initErr = EnsureLevelSetCategory(setDir, category, out recipesDir, out categoryDir, out config);
        if (initErr != null)
            return initErr;

        var updating = !string.IsNullOrEmpty(dto.updateAssetPath);
        CustomRecipeSO recipe = null;
        string assetPath = categoryDir + "/" + recipeName + ".asset";
        int uid = 0;
        if (updating)
        {
            recipe = AssetDatabase.LoadAssetAtPath<CustomRecipeSO>(dto.updateAssetPath);
            if (recipe == null)
                return "要更新的汉堡不存在：" + dto.updateAssetPath;
            if (!IsFinishedBurgerRecipe(recipe))
                return "指定资产不是成品汉堡菜谱。";
            assetPath = dto.updateAssetPath;
            uid = recipe.uID;
            if (!string.Equals(recipe.recipeName, recipeName, StringComparison.Ordinal)
                && !LayoutEditorLevelAdminApi.GloballyUniqueRecipeName(recipeName))
                return "菜谱名称「" + recipeName + "」已被使用。";
        }
        else
        {
            if (!LayoutEditorLevelAdminApi.GloballyUniqueRecipeName(recipeName))
                return "菜谱名称「" + recipeName + "」已被使用。";
            if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
                return "已存在同名菜谱文件：" + recipeName;
        }

        var shellDef = AssetDatabase.LoadAssetAtPath<CustomRecipeOptionalBurgerSO>(dto.definitionAssetPath);
        if (shellDef == null)
            return "参考组装定义不存在：" + dto.definitionAssetPath;
        if (shellDef.bunSO == null && string.IsNullOrEmpty(FindFirstBunId(dto.layerIds)))
            return "堆叠中至少包含一层汉堡面包。";

        // 解析堆叠层（顺序保持，含面包 + 夹心）
        var layers = new List<ScriptableObject>();
        var unresolved = new List<string>();
        ScriptableObject firstBun = null;
        foreach (var id in dto.layerIds)
        {
            if (string.IsNullOrEmpty(id))
                continue;
            var layerSo = LayoutEditorLevelAdminApi.FindPseudoPrefabOrCustomRecipe(id);
            if (layerSo == null)
                unresolved.Add(id);
            else
            {
                layers.Add(layerSo);
                if (firstBun == null && IsBurgerBunId(id))
                    firstBun = layerSo;
            }
        }
        if (unresolved.Count > 0)
            return "以下层无法解析：" + string.Join("、", unresolved.ToArray());
        if (firstBun == null)
            return "堆叠中至少包含一层汉堡面包。";

        string[] fillerWarnings;
        var fillerErr = ValidateFillerLayers(layers, out fillerWarnings);
        if (fillerErr != null)
            return fillerErr;
        result.warnings = fillerWarnings;

        if (firstBun as PseudoPrefabSO == null)
            return "汉堡面包必须是 PseudoPrefabSO 食材：" + firstBun.name;

        if (updating)
        {
            Undo.RecordObject(recipe, "Update Burger Recipe");
            recipe.recipeName = recipeName;
            recipe.score = dto.score;
            recipe.platingStepSO = shellDef.platingStepSO;
            recipe.modelSO = shellDef.modelSO;
            recipe.compositionSOs = layers.ToArray();
            EditorUtility.SetDirty(recipe);
        }
        else
        {
            do
            {
                uid = config.uidPrefix * 1000 + config.nextSequence;
                config.nextSequence++;
            } while (LayoutEditorLevelAdminApi.IsUidConflicting(uid));
            EditorUtility.SetDirty(config);

            recipe = ScriptableObject.CreateInstance<CustomRecipeSO>();
            recipe.type = CustomRecipeSO.RecipeType.Composite;
            recipe.recipeName = recipeName;
            recipe.uID = uid;
            recipe.score = dto.score;
            recipe.platingStepSO = shellDef.platingStepSO;
            recipe.modelSO = shellDef.modelSO;
            recipe.compositionSOs = layers.ToArray();
            recipe.optionalSOs = new ScriptableObject[0];
            AssetDatabase.CreateAsset(recipe, assetPath);
            EditorUtility.SetDirty(recipe);
        }

        if (!string.IsNullOrEmpty(dto.iconBase64))
        {
            var iconErr = LayoutEditorLevelAdminApi.UploadCustomRecipeIcon(new CustomRecipeUploadDto
            {
                recipeAssetPath = assetPath,
                base64 = dto.iconBase64,
            });
            if (!string.IsNullOrEmpty(iconErr))
            {
                result.iconError = iconErr;
                LayoutEditorLog.LogWarning("[Burger] 图标设置失败（菜谱已保存）：" + iconErr);
            }
        }
        else
        {
            LayoutEditorLevelAdminApi.TryRelinkRecipeIconFromDisk(assetPath);
        }

        recipe = AssetDatabase.LoadAssetAtPath<CustomRecipeSO>(assetPath);
        if (recipe == null)
            return "汉堡保存后无法重新加载：" + assetPath;

        LayoutEditorLevelAdminApi.AddCustomRecipeName(
            LayoutEditorLevelAdminApi.CustomRecipeNamesPath(null, assetPath),
            recipeName, dto.nameZh, dto.nameEn);

        AssetDatabase.SaveAssets();

        result.ok = true;
        result.updated = updating;
        result.assetPath = assetPath;
        result.guid = AssetDatabase.AssetPathToGUID(assetPath);
        result.uID = uid;
        result.addedToDefinition = new string[0];
        LayoutEditorLog.Log("[Burger] " + (updating ? "更新" : "创建") + "汉堡 " + recipeName + "（关卡集 " + setName
            + "，uID " + uid + "，" + layers.Count + " 层）");
        return null;
    }

    /// <summary>确保关卡集 custom_recipes 与分类目录存在，返回配置 SO。</summary>
    private static string EnsureLevelSetCategory(string setDir, string category,
        out string recipesDir, out string categoryDir, out CustomRecipeConfigSO config)
    {
        recipesDir = setDir + "/custom_recipes";
        categoryDir = recipesDir + "/" + category;
        config = null;
        if (!LayoutEditorLevelAdminApi.AssetFolderExists(recipesDir))
            return "请先在该关卡集的自定义菜谱页初始化配置。";
        config = AssetDatabase.LoadAssetAtPath<CustomRecipeConfigSO>(recipesDir + "/CustomRecipeConfig.asset");
        if (config == null)
            return "关卡集自定义菜谱配置丢失：" + recipesDir + "/CustomRecipeConfig.asset";
        if (!LayoutEditorLevelAdminApi.AssetFolderExists(categoryDir))
        {
            AssetDatabase.CreateFolder(recipesDir, category);
            var cats = new List<CustomRecipeConfigSO.CustomRecipeCategoryEntry>(
                config.categories ?? new CustomRecipeConfigSO.CustomRecipeCategoryEntry[0]);
            if (!cats.Exists(c => c.id == category))
            {
                cats.Add(new CustomRecipeConfigSO.CustomRecipeCategoryEntry { id = category, zh = "汉堡", en = "Burger" });
                config.categories = cats.ToArray();
                EditorUtility.SetDirty(config);
            }
        }
        return null;
    }

    // ------------------------------------------------------------ 组装定义管理

    /// <summary>POST /api/burger/definition/update：容量调整 / 夹心增删 / 堆叠模型绑定。
    ///  所有数组操作保持 optionalSOs ↔ ingredientModelSOs ↔ ingredientModels 下标对齐。</summary>
    public static string UpdateDefinition(BurgerDefinitionUpdateDto dto)
    {
        if (dto == null || string.IsNullOrEmpty(dto.assetPath))
            return "缺少组装定义路径。";
        var def = AssetDatabase.LoadAssetAtPath<CustomRecipeOptionalBurgerSO>(dto.assetPath);
        if (def == null)
            return "组装定义不存在：" + dto.assetPath;

        Undo.RecordObject(def, "Burger Definition Update");

        var opts = new List<ScriptableObject>(def.optionalSOs ?? new ScriptableObject[0]);
        var modelSOs = new List<PseudoPrefabSO>(def.ingredientModelSOs ?? new PseudoPrefabSO[0]);
        var models = new List<GameObject>(def.ingredientModels ?? new GameObject[0]);
        while (modelSOs.Count < opts.Count) modelSOs.Add(null);
        while (models.Count < opts.Count) models.Add(null);

        // 1. 移除（按 guid 移除全部出现）
        if (dto.removeLayerGuids != null && dto.removeLayerGuids.Length > 0)
        {
            var remove = new HashSet<string>(dto.removeLayerGuids);
            for (int i = opts.Count - 1; i >= 0; i--)
            {
                var p = opts[i] != null ? AssetDatabase.GetAssetPath(opts[i]) : null;
                var g = string.IsNullOrEmpty(p) ? "" : AssetDatabase.AssetPathToGUID(p);
                if (!string.IsNullOrEmpty(g) && remove.Contains(g))
                {
                    opts.RemoveAt(i);
                    modelSOs.RemoveAt(i);
                    models.RemoveAt(i);
                }
            }
        }

        // 2. 追加（模型位优先继承 commonW2 组装定义的绑定，无则留空回退官方 lookup）
        if (dto.addLayerIds != null)
        {
            Dictionary<string, PseudoPrefabSO> w2ModelByGuid;
            Dictionary<string, GameObject> w2GoByGuid;
            CollectCommonW2LayerModelBindings(out w2ModelByGuid, out w2GoByGuid);
            var emptyModelByGuid = new Dictionary<string, PseudoPrefabSO>(StringComparer.Ordinal);
            var emptyGoByGuid = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            foreach (var id in dto.addLayerIds)
            {
                if (string.IsNullOrEmpty(id))
                    continue;
                var so = LayoutEditorLevelAdminApi.FindPseudoPrefabOrCustomRecipe(id);
                if (so == null)
                    return "夹心无法解析：" + id;
                PseudoPrefabSO mso;
                GameObject mgo;
                ResolveLayerModel(so, emptyModelByGuid, emptyGoByGuid, w2ModelByGuid, w2GoByGuid, out mso, out mgo);
                opts.Add(so);
                modelSOs.Add(mso);
                models.Add(mso != null ? null : mgo);
            }
        }

        // 3. 模型绑定（该食材的全部出现一并绑定/解绑）；双通道互斥：
        //    pseudo → ingredientModelSOs（bundle 指针）；local → ingredientModels（本地 prefab）
        if (dto.modelBindings != null)
        {
            foreach (var b in dto.modelBindings)
            {
                if (b == null || string.IsNullOrEmpty(b.layerGuid))
                    continue;
                var wantLocal = string.Equals(b.modelKind, "local", StringComparison.Ordinal);
                PseudoPrefabSO modelSO = null;
                GameObject modelGo = null;
                if (!string.IsNullOrEmpty(b.modelId))
                {
                    if (wantLocal)
                    {
                        modelGo = FindLocalBurgerModelPrefab(b.modelId);
                        if (modelGo == null)
                            return "本地堆叠模型无法解析：" + b.modelId;
                    }
                    else
                    {
                        modelSO = FindBurgerModelSO(b.modelId);
                        if (modelSO == null)
                            return "堆叠模型无法解析：" + b.modelId;
                    }
                }
                for (int i = 0; i < opts.Count; i++)
                {
                    var p = opts[i] != null ? AssetDatabase.GetAssetPath(opts[i]) : null;
                    var g = string.IsNullOrEmpty(p) ? "" : AssetDatabase.AssetPathToGUID(p);
                    if (g != b.layerGuid)
                        continue;
                    modelSOs[i] = modelSO;
                    models[i] = modelGo;
                }
            }
        }

        if (dto.capacity > 0)
            def.ingredientContainerCapacity = dto.capacity;

        def.optionalSOs = opts.ToArray();
        def.ingredientModelSOs = modelSOs.ToArray();
        def.ingredientModels = models.ToArray();
        EditorUtility.SetDirty(def);
        AssetDatabase.SaveAssets();
        LayoutEditorLog.Log("[Burger] 组装定义更新 " + def.name + "：可选夹心 " + opts.Count + " 项，容量 " + def.ingredientContainerCapacity);
        return null;
    }

    /// <summary>堆叠模型 PseudoPrefabSO 查找：优先 commonW2/models（Burger大全自带模型指针），
    ///  再全项目 PseudoPrefabSO 目录；仍未命中则尝试从原版模型池按需生成包装。</summary>
    private static PseudoPrefabSO FindBurgerModelSO(string id)
    {
        var dirs = new List<string>();
        var modelsDir = CommonW2BurgerDir() + "/models";
        if (LayoutEditorLevelAdminApi.AssetFolderExists(modelsDir))
            dirs.Add(modelsDir);
        dirs.AddRange(LayoutEditorLevelAdminApi.PseudoPrefabSearchFolders);
        foreach (var dir in dirs)
        {
            foreach (var a in LayoutEditorLevelAdminApi.ScanAssetsByScript(dir, LayoutEditorLevelAdminApi.PseudoPrefabScriptGuid))
            {
                if (string.Equals(Path.GetFileNameWithoutExtension(a.assetPath), id, StringComparison.Ordinal))
                    return AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(a.assetPath);
            }
        }
        // 原版模型池：选用时才落包装 SO（bundle 不可用会被 MaterializePoolModel 拒绝）
        return MaterializePoolModel(id);
    }

    /// <summary>本地堆叠模型 prefab（commonW2/models 里的自制四件套产物）。</summary>
    private static GameObject FindLocalBurgerModelPrefab(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;
        var modelsDir = CommonW2BurgerDir() + "/models";
        var path = modelsDir + "/" + id + (id.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ? "" : ".prefab");
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    private static string FindFirstBunId(string[] ids)
    {
        if (ids == null)
            return null;
        foreach (var id in ids)
        {
            if (IsBurgerBunId(id))
                return id;
        }
        return null;
    }

    private static string SanitizeId(string s)
    {
        if (string.IsNullOrEmpty(s))
            return null;
        var sb = new System.Text.StringBuilder();
        foreach (var ch in s.Trim())
        {
            if (char.IsLetterOrDigit(ch) || ch == '_')
                sb.Append(ch);
        }
        return sb.Length > 0 ? sb.ToString() : null;
    }
}
