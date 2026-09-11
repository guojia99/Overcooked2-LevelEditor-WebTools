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
        public string kind;      // "custom"（commonW2/common01 中间产物）/ "official-recipe" / "ingredient"
        public string assetPath; // custom 类候选的图标走 /api/custom-recipes/icon?assetPath=
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
        public int score;
        /// <summary>compositionSOs 的资产 id 列表（含面包底）。</summary>
        public string[] compositionIds;
    }

    [Serializable]
    public class BurgerModelDto
    {
        public string id;
        public string assetPath;
        public string prefabName;
        public string bundleName;
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
    }

    [Serializable]
    public class BurgerModelBindingDto
    {
        public string layerGuid; // optionalSO 条目 guid（该食材的全部出现一并绑定）
        public string modelId;   // PseudoPrefabSO 文件名；"" = 解绑（回退官方 lookup）
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

    /// <summary>GET /api/burger/definitions?setName=：commonW2 共享组装定义（工作台参考壳）。</summary>
    public static BurgerDefinitionListDto GetDefinitions(string setName)
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
            candidates = CollectCandidates(),
            buns = CollectBuns(),
            models = CollectModels(),
            products = CollectProducts(setName),
        };
    }

    /// <summary>汉堡面包 id 判定（ChoppedBun 系列 + 历史别名 dlc08_bun）。</summary>
    internal static bool IsBurgerBunId(string id)
    {
        if (string.IsNullOrEmpty(id))
            return false;
        if (string.Equals(id, "ChoppedBunSO", StringComparison.Ordinal))
            return true;
        if (string.Equals(id, "dlc08_bun", StringComparison.OrdinalIgnoreCase))
            return true;
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
    internal static void CollectCommonW2LayerModelBindings(
        out Dictionary<string, PseudoPrefabSO> modelByGuid,
        out Dictionary<string, GameObject> modelGoByGuid)
    {
        modelByGuid = new Dictionary<string, PseudoPrefabSO>(StringComparer.Ordinal);
        modelGoByGuid = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        var burgerDir = LayoutEditorLevelAdminApi.CommonW2RecipesDir + "/"
            + LayoutEditorLevelAdminApi.BurgerCategoryId;
        if (!LayoutEditorLevelAdminApi.AssetFolderExists(burgerDir))
            return;

        var ordered = new List<CustomRecipeOptionalBurgerSO>();
        var master = AssetDatabase.LoadAssetAtPath<CustomRecipeOptionalBurgerSO>(
            burgerDir + "/OptionalBurger.asset");
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

    /// <summary>解析单层堆叠模型：本地已有绑定优先，缺失时继承 commonW2 组装定义的绑定；
    /// modelSO 优先于直接 GameObject 引用（避免双写）。</summary>
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
                + "——其余面包沿用官方叠层规则，请统一面包";
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
            LayoutEditorLog.LogWarning("[Optional] BurgerOptional 以下中间产物夹心无堆叠模型绑定"
                + "（commonW2 与官方面包表均未覆盖，叠层将不可见，请在汉堡工作台补绑定）："
                + string.Join("、", unboundCustoms.ToArray()));

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
    /// 会按关卡实际面包覆盖），不要再默认 ChickenBurgerAssembly（其面包是 DLC08）。</summary>
    private static CustomRecipeOptionalBurgerSO FindCommonW2BurgerModelAssembly()
    {
        var burgerDir = LayoutEditorLevelAdminApi.CommonW2RecipesDir + "/"
            + LayoutEditorLevelAdminApi.BurgerCategoryId;
        if (!LayoutEditorLevelAdminApi.AssetFolderExists(burgerDir))
            return null;
        var master = AssetDatabase.LoadAssetAtPath<CustomRecipeOptionalBurgerSO>(
            burgerDir + "/OptionalBurger.asset");
        if (master != null)
            return master;
        return AssetDatabase.LoadAssetAtPath<CustomRecipeOptionalBurgerSO>(
            burgerDir + "/ChickenBurgerAssembly.asset");
    }

    private static BurgerCandidateDto[] CollectBuns()
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
                string zh, en;
                LayoutEditorManualLookup.TryGet(id, out zh, out en);
                list.Add(new BurgerCandidateDto
                {
                    guid = asset.guid,
                    id = id,
                    nameZh = string.IsNullOrEmpty(zh) ? id : zh,
                    nameEn = string.IsNullOrEmpty(en) ? id : en,
                    kind = "bun",
                    assetPath = asset.assetPath,
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
            string zh;
            if (!zhMap.TryGetValue(key, out zh) || string.IsNullOrEmpty(zh))
                zh = Path.GetFileNameWithoutExtension(asset.assetPath);
            list.Add(new BurgerProductDto
            {
                guid = asset.guid,
                id = Path.GetFileNameWithoutExtension(asset.assetPath),
                assetPath = asset.assetPath,
                recipeName = key,
                nameZh = zh,
                score = so.score,
                compositionIds = comps.ToArray(),
            });
        }
    }

    /// <summary>commonW2 models 目录内的堆叠模型指针（PseudoPrefabSO → 游戏 bundle 资产）。</summary>
    private static BurgerModelDto[] CollectModels()
    {
        var list = new List<BurgerModelDto>();
        var modelsDir = LayoutEditorLevelAdminApi.CommonW2RecipesDir + "/" + LayoutEditorLevelAdminApi.BurgerCategoryId + "/models";
        if (LayoutEditorLevelAdminApi.AssetFolderExists(modelsDir))
        {
            foreach (var asset in LayoutEditorLevelAdminApi.ScanAssetsByScript(
                         modelsDir, LayoutEditorLevelAdminApi.PseudoPrefabScriptGuid))
            {
                var so = AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(asset.assetPath);
                if (so == null)
                    continue;
                list.Add(new BurgerModelDto
                {
                    id = Path.GetFileNameWithoutExtension(asset.assetPath),
                    assetPath = asset.assetPath,
                    prefabName = so.prefabName ?? "",
                    bundleName = so.bundleName ?? "",
                });
            }
        }
        list.Sort((a, b) => string.Compare(a.id, b.id, StringComparison.Ordinal));
        return list.ToArray();
    }

    /// <summary>夹心候选：commonW2 汉堡大全各组装定义 optionalSOs 的并集（唯一 guid）。</summary>
    private static BurgerCandidateDto[] CollectCandidates()
    {
        var list = new List<BurgerCandidateDto>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var allowGuids = CollectCommonW2BurgerFillerGuids();
        if (allowGuids.Count == 0)
            return list.ToArray();

        var w2Dir = LayoutEditorLevelAdminApi.CommonW2RecipesDir;
        var orderedGuids = new List<string>(allowGuids);
        orderedGuids.Sort(StringComparer.Ordinal);

        for (int i = 0; i < orderedGuids.Count; i++)
        {
            var guid = orderedGuids[i];
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
                continue;
            var custom = AssetDatabase.LoadAssetAtPath<CustomRecipeSO>(path);
            if (custom != null && !(custom is CustomRecipeOptionalBurgerSO) && !(custom is CustomRecipeOptionalPizzaSO))
            {
                AddFillerCandidate(list, seen, guid, path, custom, "custom", w2Dir);
                continue;
            }
            var ing = AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(path);
            if (ing != null)
                AddFillerCandidate(list, seen, guid, path, ing, "ingredient", w2Dir);
        }

        list.Sort((a, b) => string.Compare(a.nameZh, b.nameZh, StringComparison.Ordinal));
        return list.ToArray();
    }

    /// <summary>commonW2/custom_recipes/burger 下全部组装定义 optionalSOs 的去重 guid 集合。</summary>
    internal static HashSet<string> CollectCommonW2BurgerFillerGuids()
    {
        var guids = new HashSet<string>(StringComparer.Ordinal);
        var burgerDir = LayoutEditorLevelAdminApi.CommonW2RecipesDir + "/"
            + LayoutEditorLevelAdminApi.BurgerCategoryId;
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

    private static void AddFillerCandidate(
        List<BurgerCandidateDto> list,
        HashSet<string> seen,
        string guid,
        string path,
        ScriptableObject so,
        string kind,
        string w2Dir)
    {
        if (!seen.Add(guid))
            return;
        var id = Path.GetFileNameWithoutExtension(path);
        string zh = id;
        string en = id;
        if (kind == "custom")
        {
            var crs = so as CustomRecipeSO;
            var key = crs != null && !string.IsNullOrEmpty(crs.recipeName) ? crs.recipeName : id;
            string tmpZh, tmpEn;
            if (LayoutEditorLevelAdminApi.TryGetCustomRecipeDisplayName(w2Dir, key, out tmpZh, out tmpEn)
                || LayoutEditorLevelAdminApi.TryGetCustomRecipeDisplayName(w2Dir, id, out tmpZh, out tmpEn))
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
        list.Add(new BurgerCandidateDto
        {
            guid = guid,
            id = id,
            nameZh = zh,
            nameEn = en,
            kind = kind,
            assetPath = path,
        });
    }

    private static string ValidateFillerLayersAllowed(IList<ScriptableObject> layers)
    {
        var allowed = CollectCommonW2BurgerFillerGuids();
        if (allowed.Count == 0)
            return "未找到 commonW2 汉堡大全组装定义，无法校验夹心层。";
        var bad = new List<string>();
        foreach (var layer in layers)
        {
            if (layer == null)
                continue;
            var lp = AssetDatabase.GetAssetPath(layer);
            var lid = string.IsNullOrEmpty(lp) ? layer.name : Path.GetFileNameWithoutExtension(lp);
            if (IsBurgerBunId(lid))
                continue;
            var lg = string.IsNullOrEmpty(lp) ? "" : AssetDatabase.AssetPathToGUID(lp);
            if (string.IsNullOrEmpty(lg) || !allowed.Contains(lg))
                bad.Add(lid);
        }
        if (bad.Count == 0)
            return null;
        return "以下夹心不在 commonW2 汉堡大全允许列表中：" + string.Join("、", bad.ToArray());
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
        result = new BurgerCreateResultDto { ok = false, addedToDefinition = new string[0] };
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

        var fillerErr = ValidateFillerLayersAllowed(layers);
        if (fillerErr != null)
            return fillerErr;

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

        // 3. 模型绑定（该食材的全部出现一并绑定/解绑）
        if (dto.modelBindings != null)
        {
            foreach (var b in dto.modelBindings)
            {
                if (b == null || string.IsNullOrEmpty(b.layerGuid))
                    continue;
                PseudoPrefabSO modelSO = null;
                if (!string.IsNullOrEmpty(b.modelId))
                {
                    modelSO = FindBurgerModelSO(b.modelId);
                    if (modelSO == null)
                        return "堆叠模型无法解析：" + b.modelId;
                }
                for (int i = 0; i < opts.Count; i++)
                {
                    var p = opts[i] != null ? AssetDatabase.GetAssetPath(opts[i]) : null;
                    var g = string.IsNullOrEmpty(p) ? "" : AssetDatabase.AssetPathToGUID(p);
                    if (g == b.layerGuid)
                    {
                        modelSOs[i] = modelSO;
                        if (modelSO != null)
                            models[i] = null; // SO 绑定优先，清掉直引避免双写
                    }
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
    ///  再全项目 PseudoPrefabSO 目录。</summary>
    private static PseudoPrefabSO FindBurgerModelSO(string id)
    {
        var dirs = new List<string>();
        var modelsDir = LayoutEditorLevelAdminApi.CommonW2RecipesDir + "/" + LayoutEditorLevelAdminApi.BurgerCategoryId + "/models";
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
        return null;
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
