using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using LevelEditorStub;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 关卡集自定义内容（食材 / 锅具 / 工作台 / 水槽 / 托盘 / 备餐容器 / 分料机）
/// 的编辑器后端：模板克隆、独立 PseudoPrefabSO、OrderNode、图标/模型、参数写入、
/// 动态 catalog 与诊断。
///
/// 铁律：
///  - 只写 Assets/LevelSets/&lt;set&gt;/custom_content/；common04 / Modules 只读。
///  - 模板资源永不修改；克隆后用 PrefabUtility.InstantiatePrefab + ReplacePrefab
///    （Unity 2017 兼容）写回。
///  - 依赖 common04 的 supplies；导出时 common04 由 LayoutEditorSetExporter 携带。
///  - C# 4.0（Unity 2017），禁止 ?. / 字符串插值 / nameof / 表达式体。
/// </summary>
public static class LayoutEditorCustomContentApi
{
    private const string LevelSetsRoot = "Assets/LevelSets";
    private const string ContentDir = "custom_content";
    private const string ConfigName = "Config.asset";

    private const string IngredientWorkable = "Assets/common04/custom_ingredient/custom_ingredient_workable/custom_ingredient_workable.prefab";
    private const string IngredientUnworkable = "Assets/common04/custom_ingredient/custom_ingredient_unworkable/custom_ingredient_unworkable.prefab";
    private const string IngredientPreparation = "Assets/common04/custom_ingredient/custom_ingredient_preparation/custom_ingredient_preparation.prefab";
    private const string NodeIngredient = "Assets/common04/custom_ingredient/custom_ingredient_unworkable/node_ingredient.asset";
    private const string NodePreparation = "Assets/common04/custom_ingredient/custom_ingredient_preparation/node_ingredient_preparation.asset";

    private const string Counter = "Assets/common04/custom_counter/custom_counter.prefab";
    private const string CounterChopping = "Assets/common04/custom_counter/custom_counter_chopping.prefab";
    private const string CounterSink = "Assets/common04/custom_counter/custom_counter_sink.prefab";

    private const string UtensilPlate = "Assets/common04/custom_utensil/custom_utensil_plate.prefab";
    private const string UtensilTray = "Assets/common04/custom_utensil/custom_utensil_tray.prefab";
    private const string UtensilCleanPlateStack = "Assets/common04/custom_utensil/custom_utensil_clean_plate_stack.prefab";
    private const string UtensilDirtyPlate = "Assets/common04/custom_utensil/custom_utensil_dirty_plate.prefab";
    private const string UtensilDirtyPlateStack = "Assets/common04/custom_utensil/custom_utensil_dirty_plate_stack.prefab";
    private const string UtensilCleanTrayStack = "Assets/common04/custom_utensil/custom_utensil_clean_tray_stack.prefab";
    private const string UtensilDirtyTray = "Assets/common04/custom_utensil/custom_utensil_dirty_tray.prefab";
    private const string UtensilDirtyTrayStack = "Assets/common04/custom_utensil/custom_utensil_dirty_tray_stack.prefab";

    private const string HotpotPot = "Assets/commonW1/prefabs/web/hotpot/web_utensil_large_pot_01.prefab";
    private const string HotpotBurner = "Assets/commonW1/prefabs/web/hotpot/web_cooking_region_floorburner.prefab";
    private const string RoastingTray = "Assets/common03/prefabs/utensils/utensil_roasting_tray.prefab";
    private const string GriddlePan = "Assets/common02/prefabs/utensils/GriddlePan.prefab";
    private const string Skewer = "Assets/common02/prefabs/utensils/Skewer.prefab";
    private const string ToastingFork = "Assets/common02/prefabs/utensils/ToastingFork.prefab";
    private const string Pot = "Assets/common01/prefabs/utensils/Pot.prefab";
    private const string Steamer = "Assets/common01/prefabs/utensils/Steamer.prefab";
    private const string Splitter = "Assets/Modules/IngredientSplitter/custom_counter_ingredient_splitter.prefab";

    private static readonly string[] PlaceableKinds = { "counter", "utensil", "sink", "tray", "splitter" };

    /// <summary>所有可选模板（kind → 模板列表）。</summary>
    public static CustomContentTemplateListDto GetTemplates(string kind)
    {
        var list = new List<CustomContentTemplateDto>();
        Add(list, "ingredient_workable", "ingredient", "可加工食材", "Workable ingredient", IngredientWorkable, "");
        Add(list, "ingredient_unworkable", "ingredient", "不可加工食材", "Unworkable ingredient", IngredientUnworkable, NodeIngredient);
        Add(list, "ingredient_preparation", "ingredient", "备餐容器食材", "Preparation ingredient", IngredientPreparation, NodePreparation);
        Add(list, "counter", "counter", "工作台", "Counter", Counter, "");
        Add(list, "counter_chopping", "counter", "切菜工作台", "Chopping counter", CounterChopping, "");
        Add(list, "counter_sink", "counter", "水槽工作台", "Sink counter", CounterSink, "");
        Add(list, "utensil_plate", "utensil", "盘子", "Plate", UtensilPlate, "");
        Add(list, "utensil_tray", "utensil", "托盘", "Tray", UtensilTray, "");
        Add(list, "utensil_hotpot", "utensil", "火锅大锅", "Hotpot", HotpotPot, "");
        Add(list, "utensil_roasting_tray", "utensil", "烤盘", "Roasting tray", RoastingTray, "");
        Add(list, "utensil_griddle_pan", "utensil", "煎盘", "Griddle pan", GriddlePan, "");
        Add(list, "utensil_skewer", "utensil", "烤串", "Skewer", Skewer, "");
        Add(list, "utensil_toasting_fork", "utensil", "烤面包叉", "Toasting fork", ToastingFork, "");
        Add(list, "utensil_pot", "utensil", "汤锅", "Pot", Pot, "");
        Add(list, "utensil_steamer", "utensil", "蒸笼", "Steamer", Steamer, "");
        Add(list, "sink", "sink", "水槽", "Sink", CounterSink, "");
        Add(list, "tray", "tray", "托盘", "Tray", UtensilTray, "");
        Add(list, "prep", "prep", "备餐容器", "Preparation container", IngredientPreparation, NodePreparation);
        Add(list, "splitter", "splitter", "分料机", "Ingredient splitter", Splitter, "");
        Add(list, "clean_plate_stack", "utensil", "干净盘堆", "Clean plate stack", UtensilCleanPlateStack, "");
        Add(list, "dirty_plate", "utensil", "脏盘", "Dirty plate", UtensilDirtyPlate, "");
        Add(list, "dirty_plate_stack", "utensil", "脏盘堆", "Dirty plate stack", UtensilDirtyPlateStack, "");
        Add(list, "clean_tray_stack", "utensil", "干净托盘堆", "Clean tray stack", UtensilCleanTrayStack, "");
        Add(list, "dirty_tray", "utensil", "脏托盘", "Dirty tray", UtensilDirtyTray, "");
        Add(list, "dirty_tray_stack", "utensil", "脏托盘堆", "Dirty tray stack", UtensilDirtyTrayStack, "");
        Add(list, "hotpot_burner", "counter", "火锅灶台", "Hotpot burner", HotpotBurner, "");

        var dto = new CustomContentTemplateListDto();
        if (!string.IsNullOrEmpty(kind))
            list.RemoveAll(delegate(CustomContentTemplateDto t) { return t.kind != kind; });
        dto.templates = list.ToArray();
        return dto;
    }

    private static void Add(List<CustomContentTemplateDto> list, string id, string kind, string zh, string en,
        string assetPath, string nodePath)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(assetPath) == null)
            return;
        list.Add(new CustomContentTemplateDto
        {
            id = id,
            kind = kind,
            labelZh = zh,
            labelEn = en,
            assetPath = assetPath,
            nodePath = nodePath
        });
    }

    public static CustomContentListDto List(string setName, string kind)
    {
        var dto = new CustomContentListDto { setName = setName, items = new CustomContentItemDto[0] };
        if (string.IsNullOrEmpty(setName))
            return dto;
        var config = LoadConfig(setName);
        if (config == null || config.entries == null)
            return dto;
        var items = new List<CustomContentItemDto>();
        for (int i = 0; i < config.entries.Length; i++)
        {
            var e = config.entries[i];
            if (e == null || string.IsNullOrEmpty(e.prefabPath))
                continue;
            if (!string.IsNullOrEmpty(kind) && e.kind != kind)
                continue;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(e.prefabPath);
            if (prefab == null)
                continue;
            var warnings = new List<string>();
            if (e.kind == "ingredient" || e.kind == "prep")
            {
                if (string.IsNullOrEmpty(e.nodePath) || AssetDatabase.LoadAssetAtPath<ScriptableObject>(e.nodePath) == null)
                    warnings.Add("OrderNode 缺失");
                if (FindStandaloneSo(e.prefabPath) == null)
                    warnings.Add("PseudoPrefabSO 缺失");
            }
            if (!BundleFileExists(setName + "/" + ContentDir))
                warnings.Add("custom_content bundle 尚未构建");
            var itemDto = new CustomContentItemDto
            {
                id = e.id,
                kind = e.kind,
                nameZh = e.nameZh,
                nameEn = e.nameEn,
                templateId = e.templateId,
                assetPath = e.prefabPath,
                guid = AssetDatabase.AssetPathToGUID(e.prefabPath),
                nodePath = e.nodePath,
                iconUrl = string.IsNullOrEmpty(e.iconPath) ? "" : "/api/custom-content/icon?assetPath=" + Uri.EscapeDataString(e.prefabPath),
                bundleName = setName + "/" + ContentDir,
                warnings = warnings.ToArray()
            };
            FillItemSettings(itemDto, e);
            items.Add(itemDto);
        }
        dto.items = items.ToArray();
        return dto;
    }

    private static void FillItemSettings(CustomContentItemDto dto, CustomContentConfigSO.Entry entry)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(entry.prefabPath);
        if (prefab == null)
            return;
        var node = AssetDatabase.LoadAssetAtPath<IngredientOrderNode>(entry.nodePath);
        if (node != null)
        {
            dto.orderUid = node.m_uID;
            var platePath = node.m_platingStep != null ? AssetDatabase.GetAssetPath(node.m_platingStep) : "";
            if (!string.IsNullOrEmpty(platePath))
                dto.platingStepId = Path.GetFileNameWithoutExtension(platePath);
        }
        var prep = prefab.GetComponent<PreparationContainer>();
        if (prep != null)
        {
            dto.restrictionsGuid = GuidOf(prep.m_containerRestrictions);
            dto.cosmeticsPrefabGuid = GuidOf(prep.m_cosmeticsPrefab);
        }
        var workable = prefab.GetComponent<WorkableItem>();
        if (workable != null)
            dto.nextPrefabGuid = GuidOf(workable.m_nextPrefab);

        var utensil = prefab.GetComponent<PseudoPrefabCookingUtensilStub>();
        if (utensil != null)
        {
            dto.capacity = utensil.capacity;
            if (utensil.allowedCookingStationTypes != null)
            {
                var st = new List<string>();
                for (int i = 0; i < utensil.allowedCookingStationTypes.Length; i++)
                    st.Add(utensil.allowedCookingStationTypes[i].ToString());
                dto.stationTypes = st.ToArray();
            }
            if (utensil.allowedIngredientSOs != null)
            {
                var gs = new List<string>();
                for (int i = 0; i < utensil.allowedIngredientSOs.Length; i++)
                {
                    var g = GuidOf(utensil.allowedIngredientSOs[i]);
                    if (!string.IsNullOrEmpty(g)) gs.Add(g);
                }
                dto.allowedIngredientGuids = gs.ToArray();
            }
            if (utensil.models != null)
            {
                var gs = new List<string>();
                for (int i = 0; i < utensil.models.Length; i++)
                {
                    var g = GuidOf(utensil.models[i]);
                    if (!string.IsNullOrEmpty(g)) gs.Add(g);
                }
                dto.modelGuids = gs.ToArray();
            }
            if (utensil.modelSOs != null)
            {
                var gs = new List<string>();
                for (int i = 0; i < utensil.modelSOs.Length; i++)
                {
                    var g = GuidOf(utensil.modelSOs[i]);
                    if (!string.IsNullOrEmpty(g)) gs.Add(g);
                }
                dto.modelSoGuids = gs.ToArray();
            }
        }

        ReadSetupSo(prefab, "SetupCustomIngredientStub", "ingredientContentUIPrefabSO", ref dto.ingredientContentUiGuid);
        ReadSetupSo(prefab, "SetupCustomIngredientStub", "progressUIPrefabSO", ref dto.progressUiGuid);
        ReadSetupSo(prefab, "SetupCustomCounterStub", "progressUIPrefabSO", ref dto.progressUiGuid);
        ReadSetupSo(prefab, "SetupCustomCounterStub", "fireEffectPrefabSO", ref dto.fireEffectGuid);
        ReadSetupSo(prefab, "SetupCustomCounterStub", "chopPFXSO", ref dto.chopPfxGuid);
        ReadSetupSo(prefab, "SetupCustomCounterStub", "tutorialIconUISO", ref dto.tutorialIconGuid);
        ReadSetupSo(prefab, "SetupCustomUtensilStub", "ingredientContentUIPrefabSO", ref dto.ingredientContentUiGuid);
        ReadSetupSo(prefab, "SetupCustomUtensilStub", "sludgeSO", ref dto.sludgeGuid);
        ReadSetupSo(prefab, "SetupCustomUtensilStub", "spawnEffectSO", ref dto.spawnEffectGuid);
        ReadSetupSo(prefab, "SetupCustomUtensilStub", "progressUIPrefabSO", ref dto.progressUiGuid);
        ReadSetupSo(prefab, "SetupCustomSinkStub", "cleanPlateStackSO", ref dto.cleanPlateStackGuid);
        ReadSetupGoPath(prefab, "SetupCustomSinkStub", "washingPart", ref dto.washingPartPath);
        ReadSetupGoPath(prefab, "SetupCustomSinkStub", "dryingPart", ref dto.dryingPartPath);
        ReadSetupOrderList(prefab, "SetupCustomTrayStub", ref dto.traySlotGuid);

        var splitter = prefab.GetComponent("IngredientSplitter");
        if (splitter != null)
        {
            var so = new SerializedObject(splitter);
            var cap = so.FindProperty("capacity");
            if (cap != null) dto.capacity = cap.intValue;
            var split = so.FindProperty("splitNum");
            if (split != null) dto.splitNum = split.intValue;
            var input = so.FindProperty("inputRecipe");
            if (input != null) dto.inputRecipeGuid = GuidOf(input.objectReferenceValue);
            var output = so.FindProperty("outputPrefab");
            if (output != null) dto.outputPrefabGuid = GuidOf(output.objectReferenceValue);
            var audio = so.FindProperty("splitAudioTag");
            if (audio != null && audio.enumNames != null && audio.enumValueIndex >= 0 && audio.enumValueIndex < audio.enumNames.Length)
                dto.splitAudioTag = audio.enumNames[audio.enumValueIndex];
        }
    }

    private static string GuidOf(UnityEngine.Object obj)
    {
        if (obj == null) return "";
        var path = AssetDatabase.GetAssetPath(obj);
        return string.IsNullOrEmpty(path) ? "" : AssetDatabase.AssetPathToGUID(path);
    }

    private static void ReadSetupSo(GameObject prefab, string typeName, string field, ref string guid)
    {
        var t = FindType(typeName);
        if (t == null) return;
        var comp = prefab.GetComponent(t);
        if (comp == null) return;
        var f = t.GetField(field, BindingFlags.Public | BindingFlags.Instance);
        var so = f != null ? f.GetValue(comp) as PseudoPrefabSO : null;
        var g = GuidOf(so);
        if (!string.IsNullOrEmpty(g)) guid = g;
    }

    private static void ReadSetupGoPath(GameObject prefab, string typeName, string field, ref string path)
    {
        var t = FindType(typeName);
        if (t == null) return;
        var comp = prefab.GetComponent(t);
        if (comp == null) return;
        var f = t.GetField(field, BindingFlags.Public | BindingFlags.Instance);
        var go = f != null ? f.GetValue(comp) as GameObject : null;
        if (go != null)
            path = RelativePath(prefab.transform, go.transform);
    }

    private static void ReadSetupOrderList(GameObject prefab, string typeName, ref string guid)
    {
        var t = FindType(typeName);
        if (t == null) return;
        var comp = prefab.GetComponent(t);
        if (comp == null) return;
        var f = t.GetField("slots", BindingFlags.Public | BindingFlags.Instance);
        var arr = f != null ? f.GetValue(comp) as Array : null;
        if (arr != null && arr.Length > 0)
        {
            var g = GuidOf(arr.GetValue(0) as UnityEngine.Object);
            if (!string.IsNullOrEmpty(g)) guid = g;
        }
    }

    private static string RelativePath(Transform root, Transform child)
    {
        if (root == null || child == null) return "";
        var parts = new List<string>();
        var t = child;
        while (t != null && t != root)
        {
            parts.Add(t.name);
            t = t.parent;
        }
        parts.Reverse();
        return string.Join("/", parts.ToArray());
    }

    public static string Create(CustomContentEditDto dto)
    {
        if (dto == null || string.IsNullOrEmpty(dto.setName) || string.IsNullOrEmpty(dto.id) || string.IsNullOrEmpty(dto.kind))
            return "缺少 setName / id / kind。";
        var setName = Sanitize(dto.setName);
        var id = Sanitize(dto.id);
        if (string.IsNullOrEmpty(setName) || string.IsNullOrEmpty(id))
            return "标识只能包含字母数字和下划线。";
        var allowedKinds = new[] { "ingredient", "counter", "utensil", "sink", "tray", "prep", "splitter" };
        if (Array.IndexOf(allowedKinds, dto.kind) < 0)
            return "未知自定义类型：" + dto.kind;
        var template = FindTemplate(dto.kind, dto.templateId);
        if (template == null)
            return "未找到模板：" + (dto.templateId ?? "(空)");

        if (!AssetDatabase.IsValidFolder(LevelSetsRoot + "/" + setName))
            return "关卡集不存在：" + setName;
        EnsureConfig(setName);
        var root = Root(setName);
        SetBundle(root, setName + "/" + ContentDir);
        if (!AssetDatabase.IsValidFolder(root))
            return "无法创建自定义内容目录：" + root;
        var itemDir = root + "/" + dto.kind + "/" + id;
        if (AssetDatabase.IsValidFolder(itemDir))
            return "自定义内容已存在：" + dto.kind + "/" + id;
        EnsureFolder(root, dto.kind);
        EnsureFolder(root + "/" + dto.kind, id);

        var prefabPath = itemDir + "/" + id + ".prefab";
        try
        {
            if (!AssetDatabase.CopyAsset(template.assetPath, prefabPath))
                return "复制模板失败：" + template.assetPath;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
                return "模板复制后无法加载 prefab。";

            // OrderNode：食材/备餐容器从模板 node 复制，保证 icon/uID/plating 独立。
            string nodePath = "";
            string workableNextPrefabPath = "";
            if (!string.IsNullOrEmpty(template.nodePath))
            {
                nodePath = itemDir + "/" + id + "_Node.asset";
                if (!AssetDatabase.CopyAsset(template.nodePath, nodePath))
                    return "复制 OrderNode 失败：" + template.nodePath;
            }
            // 可加工食材：同时克隆 unworkable 形态并生成独立 node，WorkableItem
            // 的 m_nextPrefab 指向新形态（否则切完仍回到模板番茄/汉堡）。
            if (dto.kind == "ingredient" && template.id == "ingredient_workable")
            {
                nodePath = itemDir + "/" + id + "_Node.asset";
                if (!AssetDatabase.CopyAsset(NodeIngredient, nodePath))
                    return "复制食材 OrderNode 失败。";
                workableNextPrefabPath = itemDir + "/" + id + "_Unworkable.prefab";
                if (!AssetDatabase.CopyAsset(IngredientUnworkable, workableNextPrefabPath))
                    return "复制不可加工食材模板失败。";
                var unworkableAsset = AssetDatabase.LoadAssetAtPath<GameObject>(workableNextPrefabPath);
                var unworkableInstance = unworkableAsset != null
                    ? PrefabUtility.InstantiatePrefab(unworkableAsset) as GameObject
                    : null;
                if (unworkableInstance != null)
                {
                    try
                    {
                        ApplyNode(unworkableInstance, nodePath, dto);
                        PrefabUtility.ReplacePrefab(unworkableInstance, unworkableAsset, ReplacePrefabOptions.ReplaceNameBased);
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(unworkableInstance);
                    }
                }
            }

            // 独立 PseudoPrefabSO：避免 embedded SO 无法按 GUID 往返。
            var soPath = itemDir + "/" + id + "_SO.asset";
            var so = ScriptableObject.CreateInstance<PseudoPrefabSO>();
            so.prefabName = id;
            so.bundleName = "";
            so.assetPath = "";
            so.asset = prefab;
            AssetDatabase.CreateAsset(so, soPath);

            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
                return "实例化模板失败。";
            try
            {
                var baseStub = instance.GetComponent<PseudoPrefabStub>();
                if (baseStub != null)
                    baseStub.pseudoPrefabSO = so;
                ApplyNode(instance, nodePath, dto);
                ApplyCommonSettings(instance, dto);
                if (!string.IsNullOrEmpty(workableNextPrefabPath))
                {
                    var workable = instance.GetComponent<WorkableItem>();
                    if (workable != null)
                    {
                        var next = AssetDatabase.LoadAssetAtPath<GameObject>(workableNextPrefabPath);
                        if (next != null)
                        {
                            var wso = new SerializedObject(workable);
                            var p = wso.FindProperty("m_nextPrefab");
                            if (p != null)
                            {
                                p.objectReferenceValue = next;
                                wso.ApplyModifiedProperties();
                            }
                        }
                    }
                }
                PrefabUtility.ReplacePrefab(instance, prefab, ReplacePrefabOptions.ReplaceNameBased);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }

            var config = EnsureConfig(setName);
            var entry = new CustomContentConfigSO.Entry
            {
                id = id,
                kind = dto.kind,
                nameZh = string.IsNullOrEmpty(dto.nameZh) ? id : dto.nameZh,
                nameEn = string.IsNullOrEmpty(dto.nameEn) ? id : dto.nameEn,
                templateId = template.id,
                prefabPath = prefabPath,
                nodePath = nodePath,
                createdAt = DateTime.UtcNow.ToString("o"),
                updatedAt = DateTime.UtcNow.ToString("o")
            };
            AddOrUpdateEntry(config, entry);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            SetBundle(prefabPath, setName + "/" + ContentDir);
            if (!string.IsNullOrEmpty(workableNextPrefabPath))
                SetBundle(workableNextPrefabPath, setName + "/" + ContentDir);
            if (!string.IsNullOrEmpty(nodePath))
                SetBundle(nodePath, setName + "/" + ContentDir);
            SetBundle(soPath, setName + "/" + ContentDir);
            if (!string.IsNullOrEmpty(dto.iconBase64))
                SaveIcon(setName, entry, dto);
            if (dto.modelFiles != null && dto.modelFiles.Length > 0)
                SaveModel(setName, entry, dto);
            LayoutEditorLevelAdminApi.InvalidatePseudoPrefabSearchFolders();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return null;
        }
        catch (Exception ex)
        {
            return "创建自定义内容失败：" + ex.Message;
        }
    }

    public static string Update(CustomContentEditDto dto)
    {
        if (dto == null || string.IsNullOrEmpty(dto.assetPath))
            return "缺少 assetPath。";
        if (dto.assetPath.IndexOf("/" + ContentDir + "/", StringComparison.Ordinal) < 0)
            return "只允许更新 custom_content 内的资产。";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(dto.assetPath);
        if (prefab == null)
            return "未找到 prefab：" + dto.assetPath;
        var setName = SetNameFromPath(dto.assetPath);
        var itemDir = Path.GetDirectoryName(dto.assetPath).Replace("\\", "/");
        var id = Path.GetFileNameWithoutExtension(dto.assetPath);
        try
        {
            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
                return "实例化 prefab 失败。";
            try
            {
                ApplyCommonSettings(instance, dto);
                PrefabUtility.ReplacePrefab(instance, prefab, ReplacePrefabOptions.ReplaceNameBased);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }

            var config = EnsureConfig(setName);
            var entry = FindEntry(config, dto.assetPath);
            if (entry != null)
            {
                if (!string.IsNullOrEmpty(dto.nameZh)) entry.nameZh = dto.nameZh;
                if (!string.IsNullOrEmpty(dto.nameEn)) entry.nameEn = dto.nameEn;
                entry.updatedAt = DateTime.UtcNow.ToString("o");
                if (!string.IsNullOrEmpty(entry.nodePath))
                {
                    var node = AssetDatabase.LoadAssetAtPath<IngredientOrderNode>(entry.nodePath);
                    if (node != null)
                    {
                        if (dto.orderUid > 0) node.m_uID = dto.orderUid;
                        ApplyNodeIcon(node, entry);
                        if (!string.IsNullOrEmpty(dto.platingStepId))
                        {
                            var plating = FindPlatingStep(dto.platingStepId);
                            if (plating != null) node.m_platingStep = plating;
                        }
                        EditorUtility.SetDirty(node);
                    }
                }
                if (!string.IsNullOrEmpty(dto.iconBase64))
                    SaveIcon(setName, entry, dto);
                if (dto.modelFiles != null && dto.modelFiles.Length > 0)
                    SaveModel(setName, entry, dto);
                EditorUtility.SetDirty(config);
            }
            LayoutEditorLevelAdminApi.InvalidatePseudoPrefabSearchFolders();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return null;
        }
        catch (Exception ex)
        {
            return "更新自定义内容失败：" + ex.Message;
        }
    }

    public static string Delete(string setName, string kind, string id)
    {
        if (string.IsNullOrEmpty(setName) || string.IsNullOrEmpty(kind) || string.IsNullOrEmpty(id))
            return "缺少 setName / kind / id。";
        var safeKind = Sanitize(kind);
        var safeId = Sanitize(id);
        if (string.IsNullOrEmpty(safeKind) || string.IsNullOrEmpty(safeId))
            return "标识非法。";
        var itemDir = Root(setName) + "/" + safeKind + "/" + safeId;
        if (!AssetDatabase.IsValidFolder(itemDir))
            return "自定义内容不存在。";
        var config = LoadConfig(setName);
        var prefabPath = itemDir + "/" + safeId + ".prefab";
        if (config != null && config.entries != null)
        {
            for (int i = 0; i < config.entries.Length; i++)
            {
                var e = config.entries[i];
                if (e != null && e.prefabPath == prefabPath)
                {
                    var list = new List<CustomContentConfigSO.Entry>(config.entries);
                    list.RemoveAt(i);
                    config.entries = list.ToArray();
                    EditorUtility.SetDirty(config);
                    break;
                }
            }
        }
        if (!AssetDatabase.DeleteAsset(itemDir))
            return "删除目录失败。";
        LayoutEditorLevelAdminApi.InvalidatePseudoPrefabSearchFolders();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return null;
    }

    public static CustomContentCatalogDto GetCatalog(string setName)
    {
        var dto = new CustomContentCatalogDto { setName = setName, generatedAt = DateTime.UtcNow.ToString("o"), items = new CustomContentCatalogItemDto[0] };
        if (string.IsNullOrEmpty(setName))
            return dto;
        var list = new List<CustomContentCatalogItemDto>();
        var config = LoadConfig(setName);
        if (config == null || config.entries == null)
            return dto;
        for (int i = 0; i < config.entries.Length; i++)
        {
            var e = config.entries[i];
            if (e == null || string.IsNullOrEmpty(e.prefabPath))
                continue;
            if (Array.IndexOf(PlaceableKinds, e.kind) < 0)
                continue;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(e.prefabPath);
            if (prefab == null)
                continue;
            var category = e.kind == "utensil" || e.kind == "tray"
                ? "utensils/cooking"
                : (e.kind == "sink" ? "counters/sinks" : "counters/prep");
            list.Add(new CustomContentCatalogItemDto
            {
                id = e.id,
                guid = AssetDatabase.AssetPathToGUID(e.prefabPath),
                assetPath = e.prefabPath,
                category = category,
                nameZh = e.nameZh,
                nameEn = e.nameEn,
                defaultParent = e.kind == "utensil" || e.kind == "tray" ? "Design/Utensils" : "Design/Counters",
                footprint = FootprintOf(e),
                needsStub = true,
                customKind = e.kind,
                customStubKind = (e.kind == "utensil" && IsCookingUtensilTemplate(e.templateId))
                    ? "CookingUtensil" : ""
            });
        }
        dto.items = list.ToArray();
        return dto;
    }

    /// <summary>写回/导出 footprint：custom_content 生成 id 不进入静态表，按 Config 的 kind 查。</summary>
    public static LayoutFootprint GetFootprintForAssetPath(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath) || assetPath.IndexOf("/" + ContentDir + "/", StringComparison.Ordinal) < 0)
            return null;
        var config = LoadConfig(SetNameFromPath(assetPath));
        var entry = FindEntry(config, assetPath);
        return entry == null ? null : FootprintOf(entry);
    }

    public static CustomContentMetaDto GetMeta()
    {
        var dto = new CustomContentMetaDto();
        var names = Enum.GetNames(typeof(LevelEditorStub.PseudoPrefabCookingUtensilStub.CookingStationType));
        var list = new List<CustomContentStationTypeDto>();
        for (int i = 0; i < names.Length; i++)
        {
            if (names[i] == "Invalid")
                continue;
            list.Add(new CustomContentStationTypeDto
            {
                name = names[i],
                labelZh = StationLabelZh(names[i]),
                labelEn = names[i]
            });
        }
        dto.stationTypes = list.ToArray();
        return dto;
    }

    /// <summary>表单选项：所有 guid 字段都从这些列表里选，前端不再让用户手输。</summary>
    public static CustomContentOptionsDto GetOptions(string setName)
    {
        var dto = new CustomContentOptionsDto { setName = setName, audioTags = new string[0] };
        var prefabRoots = new List<string>();
        var soRoots = new List<string>();
        var modelSoRoots = new List<string>();
        var lookupRoots = new List<string>();
        var setRoot = Root(setName);
        if (!string.IsNullOrEmpty(setName) && AssetDatabase.IsValidFolder(setRoot))
        {
            prefabRoots.Add(setRoot);
            soRoots.Add(setRoot);
            modelSoRoots.Add(setRoot);
            lookupRoots.Add(setRoot);
        }
        AddIfFolder(prefabRoots, "Assets/common04/custom_ingredient");
        AddIfFolder(prefabRoots, "Assets/common04/custom_counter");
        AddIfFolder(prefabRoots, "Assets/common04/custom_utensil");
        AddIfFolder(prefabRoots, "Assets/commonW1/prefabs/web/hotpot");
        AddIfFolder(soRoots, "Assets/common04/_supplies");
        AddIfFolder(soRoots, "Assets/common04/custom_ingredient");
        AddIfFolder(soRoots, "Assets/commonW1/pseudo_prefab_so/web/hotpot");
        AddIfFolder(modelSoRoots, "Assets/common03/food/Recipes_Patch/model_so");
        AddIfFolder(lookupRoots, "Assets/common04/custom_ingredient");
        AddIfFolder(lookupRoots, "Assets/commonW2");

        dto.prefabs = ScanRefs(prefabRoots, "t:GameObject", "prefab");
        dto.soRefs = ScanRefs(soRoots, "t:PseudoPrefabSO", "so");
        dto.modelPrefabs = dto.prefabs;
        dto.modelSOs = ScanRefs(modelSoRoots, "t:PseudoPrefabSO", "modelSO");
        dto.orderLists = ScanRefs(new List<string>(new[] { "Assets/common04/custom_utensil", setRoot }),
            "t:OrderToPrefabListSO", "orderList");
        dto.lookups = ScanRefs(lookupRoots, "t:OrderToPrefabLookup", "lookup");

        var recipes = new List<CustomContentRefDto>();
        var recipeList = LayoutEditorCatalogApi.ScanRecipes(setName);
        if (recipeList != null && recipeList.recipes != null)
        {
            for (int i = 0; i < recipeList.recipes.Length; i++)
            {
                var r = recipeList.recipes[i];
                if (r == null || !r.isCustom)
                    continue;
                recipes.Add(new CustomContentRefDto
                {
                    id = r.id,
                    nameZh = string.IsNullOrEmpty(r.nameZh) ? r.id : r.nameZh,
                    nameEn = r.nameEn,
                    guid = r.guid,
                    assetPath = r.assetPath,
                    kind = r.type
                });
            }
        }
        dto.recipes = recipes.ToArray();

        var plating = new List<CustomContentRefDto>();
        var refs = LayoutEditorLevelAdminApi.GetCustomRecipeReferences(setName);
        if (refs != null && refs.platingContainers != null)
        {
            for (int i = 0; i < refs.platingContainers.Length; i++)
            {
                var p = refs.platingContainers[i];
                plating.Add(new CustomContentRefDto
                {
                    id = p.id,
                    nameZh = string.IsNullOrEmpty(p.nameZh) ? p.id : p.nameZh,
                    nameEn = p.nameEn,
                    guid = p.guid,
                    assetPath = p.assetPath,
                    kind = "plating"
                });
            }
        }
        dto.plating = plating.ToArray();
        dto.audioTags = Enum.GetNames(typeof(LevelEditorStub.LevelInfoSO.GameOneShotAudioTag));
        return dto;
    }

    public static CustomContentHierarchyDto GetHierarchy(string assetPath)
    {
        var dto = new CustomContentHierarchyDto { assetPath = assetPath, paths = new string[0] };
        var prefab = string.IsNullOrEmpty(assetPath) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (prefab == null)
            return dto;
        var paths = new List<string>();
        CollectPaths(prefab.transform, "", paths, 0);
        dto.paths = paths.ToArray();
        return dto;
    }

    private static void CollectPaths(Transform root, string prefix, List<string> paths, int depth)
    {
        if (root == null || depth > 8)
            return;
        for (int i = 0; i < root.childCount; i++)
        {
            var c = root.GetChild(i);
            var p = string.IsNullOrEmpty(prefix) ? c.name : prefix + "/" + c.name;
            paths.Add(p);
            CollectPaths(c, p, paths, depth + 1);
        }
    }

    private static void AddIfFolder(List<string> roots, string path)
    {
        if (AssetDatabase.IsValidFolder(path) && !roots.Contains(path))
            roots.Add(path);
    }

    private static CustomContentRefDto[] ScanRefs(List<string> roots, string filter, string kind)
    {
        var list = new List<CustomContentRefDto>();
        var seen = new HashSet<string>();
        for (int i = 0; i < roots.Count; i++)
        {
            var root = roots[i];
            if (string.IsNullOrEmpty(root) || !AssetDatabase.IsValidFolder(root))
                continue;
            foreach (var guid in AssetDatabase.FindAssets(filter, new[] { root }))
            {
                if (!seen.Add(guid))
                    continue;
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var id = Path.GetFileNameWithoutExtension(path);
                var so = AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(path);
                string zh = id, en = id;
                if (so != null)
                {
                    if (!string.IsNullOrEmpty(so.prefabName)) en = so.prefabName;
                    if (!string.IsNullOrEmpty(so.name)) zh = so.name;
                }
                list.Add(new CustomContentRefDto
                {
                    id = id,
                    nameZh = zh,
                    nameEn = en,
                    guid = guid,
                    assetPath = path,
                    kind = kind
                });
            }
        }
        list.Sort(delegate(CustomContentRefDto a, CustomContentRefDto b)
        {
            return string.Compare(a.nameZh, b.nameZh, StringComparison.Ordinal);
        });
        return list.ToArray();
    }

    public static string ResolveIconFile(string prefabPath, out string absPath, out string contentType)
    {
        absPath = null;
        contentType = "image/png";
        if (string.IsNullOrEmpty(prefabPath))
            return "缺少 assetPath。";
        var dir = Path.GetDirectoryName(prefabPath).Replace("\\", "/");
        var id = Path.GetFileNameWithoutExtension(prefabPath);
        var png = dir + "/" + id + "_Icon.png";
        if (File.Exists(png))
        {
            absPath = AbsPath(png);
            return null;
        }
        var config = LoadConfig(SetNameFromPath(prefabPath));
        var entry = FindEntry(config, prefabPath);
        if (entry != null && !string.IsNullOrEmpty(entry.iconPath) && File.Exists(entry.iconPath))
        {
            absPath = AbsPath(entry.iconPath);
            return null;
        }
        return "图标不存在。";
    }

    public static string ResolveModelFile(string prefabPath, string fileName, out string absPath, out string contentType)
    {
        absPath = null;
        contentType = "application/octet-stream";
        if (string.IsNullOrEmpty(prefabPath) || string.IsNullOrEmpty(fileName))
            return "缺少参数。";
        var safe = SanitizeFileName(fileName);
        if (string.IsNullOrEmpty(safe))
            return "文件名非法。";
        var dir = Path.GetDirectoryName(prefabPath).Replace("\\", "/") + "/models";
        var path = dir + "/" + safe;
        if (!File.Exists(AbsPath(path)))
            return "模型文件不存在。";
        absPath = AbsPath(path);
        var ext = Path.GetExtension(safe).ToLowerInvariant();
        if (ext == ".obj") contentType = "text/plain";
        else if (ext == ".fbx") contentType = "application/octet-stream";
        else if (ext == ".png") contentType = "image/png";
        else if (ext == ".jpg" || ext == ".jpeg") contentType = "image/jpeg";
        else contentType = "application/octet-stream";
        return null;
    }

    public static CustomRecipeModelSourceDto GetModelSource(string prefabPath)
    {
        var dto = new CustomRecipeModelSourceDto { dirAssetPath = "", modelFile = "", files = new string[0] };
        if (string.IsNullOrEmpty(prefabPath))
            return dto;
        var dir = Path.GetDirectoryName(prefabPath).Replace("\\", "/") + "/models";
        dto.dirAssetPath = dir;
        if (!AssetDatabase.IsValidFolder(dir))
            return dto;
        var files = new List<string>();
        var abs = AbsPath(dir);
        if (Directory.Exists(abs))
        {
            foreach (var f in Directory.GetFiles(abs))
            {
                var n = Path.GetFileName(f);
                if (n.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                    continue;
                files.Add(n);
                if (string.IsNullOrEmpty(dto.modelFile) && (n.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)
                    || n.EndsWith(".obj", StringComparison.OrdinalIgnoreCase)))
                    dto.modelFile = n;
            }
        }
        files.Sort(StringComparer.OrdinalIgnoreCase);
        dto.files = files.ToArray();
        return dto;
    }

    // -------------------------------------------------------------
    // 内部：模板/配置/克隆/参数
    // -------------------------------------------------------------

    private static CustomContentTemplateDto FindTemplate(string kind, string templateId)
    {
        var all = GetTemplates(kind).templates;
        for (int i = 0; i < all.Length; i++)
        {
            if (string.IsNullOrEmpty(templateId) || all[i].id == templateId)
                return all[i];
        }
        return all.Length > 0 ? all[0] : null;
    }

    private static string Root(string setName)
    {
        return LevelSetsRoot + "/" + setName + "/" + ContentDir;
    }

    private static string AbsPath(string assetPath)
    {
        var root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        return Path.GetFullPath(Path.Combine(root, assetPath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static void EnsureFolder(string parent, string child)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + child))
            AssetDatabase.CreateFolder(parent, child);
    }

    private static CustomContentConfigSO EnsureConfig(string setName)
    {
        var root = Root(setName);
        if (!AssetDatabase.IsValidFolder(root))
        {
            if (!AssetDatabase.IsValidFolder(LevelSetsRoot + "/" + setName))
                return null;
            AssetDatabase.CreateFolder(LevelSetsRoot + "/" + setName, ContentDir);
        }
        var path = root + "/" + ConfigName;
        var config = AssetDatabase.LoadAssetAtPath<CustomContentConfigSO>(path);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<CustomContentConfigSO>();
            AssetDatabase.CreateAsset(config, path);
            var imp = AssetImporter.GetAtPath(path);
            if (imp != null)
            {
                imp.assetBundleName = "";
                imp.SaveAndReimport();
            }
        }
        return config;
    }

    private static CustomContentConfigSO LoadConfig(string setName)
    {
        if (string.IsNullOrEmpty(setName))
            return null;
        return AssetDatabase.LoadAssetAtPath<CustomContentConfigSO>(Root(setName) + "/" + ConfigName);
    }

    private static void AddOrUpdateEntry(CustomContentConfigSO config, CustomContentConfigSO.Entry entry)
    {
        if (config == null || entry == null)
            return;
        var list = new List<CustomContentConfigSO.Entry>(config.entries ?? new CustomContentConfigSO.Entry[0]);
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null && list[i].prefabPath == entry.prefabPath)
            {
                list[i] = entry;
                config.entries = list.ToArray();
                return;
            }
        }
        list.Add(entry);
        config.entries = list.ToArray();
    }

    private static CustomContentConfigSO.Entry FindEntry(CustomContentConfigSO config, string prefabPath)
    {
        if (config == null || config.entries == null)
            return null;
        for (int i = 0; i < config.entries.Length; i++)
        {
            if (config.entries[i] != null && config.entries[i].prefabPath == prefabPath)
                return config.entries[i];
        }
        return null;
    }

    private static void ApplyNode(GameObject instance, string nodePath, CustomContentEditDto dto)
    {
        if (string.IsNullOrEmpty(nodePath))
            return;
        var node = AssetDatabase.LoadAssetAtPath<IngredientOrderNode>(nodePath);
        if (node == null)
            return;
        node.m_uID = dto.orderUid > 0 ? dto.orderUid : NextIngredientUid();
        if (!string.IsNullOrEmpty(dto.platingStepId))
        {
            var plating = FindPlatingStep(dto.platingStepId);
            if (plating != null)
                node.m_platingStep = plating;
        }
        EditorUtility.SetDirty(node);

        var props = instance.GetComponentsInChildren<IngredientPropertiesComponent>(true);
        for (int i = 0; i < props.Length; i++)
        {
            var so = new SerializedObject(props[i]);
            var p = so.FindProperty("m_ingredientOrderNode");
            if (p != null)
            {
                p.objectReferenceValue = node;
                so.ApplyModifiedProperties();
            }
        }
        var preps = instance.GetComponentsInChildren<PreparationContainer>(true);
        for (int i = 0; i < preps.Length; i++)
        {
            var so = new SerializedObject(preps[i]);
            var p = so.FindProperty("m_ingredientOrderNode");
            if (p != null)
            {
                p.objectReferenceValue = node;
                so.ApplyModifiedProperties();
            }
        }
    }

    private static int NextIngredientUid()
    {
        var max = 10000000;
        var guids = AssetDatabase.FindAssets("t:IngredientOrderNode");
        for (int i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var node = AssetDatabase.LoadAssetAtPath<IngredientOrderNode>(path);
            if (node != null && node.m_uID > max)
                max = node.m_uID;
        }
        return max + 1;
    }

    private static void ApplyCommonSettings(GameObject instance, CustomContentEditDto dto)
    {
        if (instance == null || dto == null)
            return;

        if (IsCookingUtensilTemplate(dto.templateId))
            EnsureCookingUtensilComponents(instance);
        var utensil = instance.GetComponent<PseudoPrefabCookingUtensilStub>();
        if (utensil != null)
        {
            var so = new SerializedObject(utensil);
            if (dto.capacity > 0)
            {
                var p = so.FindProperty("capacity");
                if (p != null) p.intValue = dto.capacity;
            }
            SetObjectArray(so, "allowedIngredientSOs", dto.allowedIngredientGuids, false);
            SetEnumArray(so, "allowedCookingStationTypes", dto.stationTypes);
            SetObjectArray(so, "models", dto.modelGuids, true);
            SetObjectArray(so, "modelSOs", dto.modelSoGuids, false);
            if (dto.capacity <= 0 && utensil.capacity <= 0)
            {
                var cap = so.FindProperty("capacity");
                if (cap != null)
                    cap.intValue = DefaultUtensilCapacity(dto.templateId);
            }
            so.ApplyModifiedProperties();
        }

        ApplySetupField(instance, "SetupCustomIngredientStub", "ingredientContentUIPrefabSO", dto.ingredientContentUiGuid, false);
        ApplySetupField(instance, "SetupCustomIngredientStub", "progressUIPrefabSO", dto.progressUiGuid, false);
        ApplySetupField(instance, "SetupCustomCounterStub", "progressUIPrefabSO", dto.progressUiGuid, false);
        ApplySetupField(instance, "SetupCustomCounterStub", "fireEffectPrefabSO", dto.fireEffectGuid, false);
        ApplySetupField(instance, "SetupCustomCounterStub", "chopPFXSO", dto.chopPfxGuid, false);
        ApplySetupField(instance, "SetupCustomCounterStub", "tutorialIconUISO", dto.tutorialIconGuid, false);
        ApplySetupField(instance, "SetupCustomUtensilStub", "ingredientContentUIPrefabSO", dto.ingredientContentUiGuid, false);
        ApplySetupField(instance, "SetupCustomUtensilStub", "sludgeSO", dto.sludgeGuid, false);
        ApplySetupField(instance, "SetupCustomUtensilStub", "spawnEffectSO", dto.spawnEffectGuid, false);
        ApplySetupField(instance, "SetupCustomUtensilStub", "progressUIPrefabSO", dto.progressUiGuid, false);
        ApplySetupField(instance, "SetupCustomSinkStub", "cleanPlateStackSO", dto.cleanPlateStackGuid, false);
        ApplySetupGameObjectPath(instance, "SetupCustomSinkStub", "washingPart", dto.washingPartPath);
        ApplySetupGameObjectPath(instance, "SetupCustomSinkStub", "dryingPart", dto.dryingPartPath);
        ApplySetupObjectArray(instance, "SetupCustomTrayStub", "slots", dto.traySlotGuid);

        var prep = instance.GetComponent<PreparationContainer>();
        if (prep != null)
        {
            var so = new SerializedObject(prep);
            if (!string.IsNullOrEmpty(dto.restrictionsGuid))
            {
                var p = so.FindProperty("m_containerRestrictions");
                if (p != null) p.objectReferenceValue = LoadAsset<OrderToPrefabLookup>(dto.restrictionsGuid);
            }
            if (!string.IsNullOrEmpty(dto.cosmeticsPrefabGuid))
            {
                var p = so.FindProperty("m_cosmeticsPrefab");
                if (p != null) p.objectReferenceValue = LoadAsset<GameObject>(dto.cosmeticsPrefabGuid);
            }
            so.ApplyModifiedProperties();
        }

        var workable = instance.GetComponent<WorkableItem>();
        if (workable != null && !string.IsNullOrEmpty(dto.nextPrefabGuid))
        {
            var so = new SerializedObject(workable);
            var p = so.FindProperty("m_nextPrefab");
            if (p != null)
            {
                p.objectReferenceValue = LoadAsset<GameObject>(dto.nextPrefabGuid);
                so.ApplyModifiedProperties();
            }
        }

        var splitter = instance.GetComponent("IngredientSplitter");
        if (splitter != null)
        {
            var so = new SerializedObject(splitter);
            if (dto.capacity > 0) SetInt(so, "capacity", dto.capacity);
            if (dto.splitNum > 0) SetInt(so, "splitNum", dto.splitNum);
            if (!string.IsNullOrEmpty(dto.inputRecipeGuid))
                SetObject(so, "inputRecipe", LoadAsset<CustomRecipeSO>(dto.inputRecipeGuid));
            if (!string.IsNullOrEmpty(dto.outputPrefabGuid))
                SetObject(so, "outputPrefab", LoadAsset<GameObject>(dto.outputPrefabGuid));
            if (!string.IsNullOrEmpty(dto.splitAudioTag))
                SetEnum(so, "splitAudioTag", "LevelEditorStub.LevelInfoSO+GameOneShotAudioTag", dto.splitAudioTag);
            so.ApplyModifiedProperties();
        }
    }

    private static int DefaultUtensilCapacity(string templateId)
    {
        if (templateId == "utensil_pot" || templateId == "utensil_skewer")
            return 3;
        if (templateId == "utensil_toasting_fork")
            return 1;
        return 4;
    }

    private static bool IsCookingUtensilTemplate(string templateId)
    {
        return templateId == "utensil_hotpot" || templateId == "utensil_roasting_tray"
            || templateId == "utensil_griddle_pan" || templateId == "utensil_skewer"
            || templateId == "utensil_toasting_fork" || templateId == "utensil_pot"
            || templateId == "utensil_steamer";
    }

    private static void EnsureCookingUtensilComponents(GameObject instance)
    {
        var baseStub = instance.GetComponent<PseudoPrefabStub>();
        var derived = instance.GetComponent<PseudoPrefabCookingUtensilStub>();
        if (derived == null)
        {
            derived = instance.AddComponent<PseudoPrefabCookingUtensilStub>();
            if (baseStub != null)
                derived.pseudoPrefabSO = baseStub.pseudoPrefabSO;
        }
        if (baseStub != null && baseStub != derived)
            UnityEngine.Object.DestroyImmediate(baseStub, true);
        var baseRuntime = instance.GetComponent<LevelEditor.PseudoPrefab>();
        var cookingRuntime = instance.GetComponent<LevelEditor.PseudoPrefabCookingUtensil>();
        if (cookingRuntime == null)
            cookingRuntime = instance.AddComponent<LevelEditor.PseudoPrefabCookingUtensil>();
        if (baseRuntime != null && baseRuntime != cookingRuntime)
            UnityEngine.Object.DestroyImmediate(baseRuntime, true);
    }

    private static void ApplySetupField(GameObject instance, string typeName, string fieldName, string guid, bool gameObject)
    {
        if (string.IsNullOrEmpty(guid))
            return;
        var t = FindType(typeName);
        if (t == null)
            return;
        var comp = instance.GetComponent(t);
        if (comp == null)
            return;
        var so = new SerializedObject(comp);
        var p = so.FindProperty(fieldName);
        if (p == null)
            return;
        p.objectReferenceValue = gameObject ? (UnityEngine.Object)LoadAsset<GameObject>(guid) : LoadAsset<PseudoPrefabSO>(guid);
        so.ApplyModifiedProperties();
    }

    private static void ApplySetupGameObjectPath(GameObject instance, string typeName, string fieldName, string path)
    {
        if (string.IsNullOrEmpty(path))
            return;
        var t = FindType(typeName);
        if (t == null) return;
        var comp = instance.GetComponent(t);
        if (comp == null) return;
        var so = new SerializedObject(comp);
        var p = so.FindProperty(fieldName);
        if (p == null) return;
        var child = instance.transform.Find(path);
        p.objectReferenceValue = child != null ? child.gameObject : null;
        so.ApplyModifiedProperties();
    }

    private static void ApplySetupObjectArray(GameObject instance, string typeName, string fieldName, string guid)
    {
        if (string.IsNullOrEmpty(guid))
            return;
        var t = FindType(typeName);
        if (t == null) return;
        var comp = instance.GetComponent(t);
        if (comp == null) return;
        var so = new SerializedObject(comp);
        var p = so.FindProperty(fieldName);
        if (p == null) return;
        p.arraySize = 1;
        var path = AssetDatabase.GUIDToAssetPath(guid);
        p.GetArrayElementAtIndex(0).objectReferenceValue =
            string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<OrderToPrefabListSO>(path);
        so.ApplyModifiedProperties();
    }

    private static void SetObjectArray(SerializedObject so, string field, string[] guids, bool gameObject)
    {
        if (guids == null)
            return;
        var p = so.FindProperty(field);
        if (p == null)
            return;
        p.arraySize = guids.Length;
        for (int i = 0; i < guids.Length; i++)
        {
            var obj = gameObject ? (UnityEngine.Object)LoadAsset<GameObject>(guids[i]) : LoadAsset<PseudoPrefabSO>(guids[i]);
            p.GetArrayElementAtIndex(i).objectReferenceValue = obj;
        }
    }

    private static void SetEnumArray(SerializedObject so, string field, string[] names)
    {
        if (names == null)
            return;
        var p = so.FindProperty(field);
        if (p == null)
            return;
        var enumType = typeof(LevelEditorStub.PseudoPrefabCookingUtensilStub.CookingStationType);
        p.arraySize = names.Length;
        for (int i = 0; i < names.Length; i++)
        {
            var value = LevelEditorStub.PseudoPrefabCookingUtensilStub.CookingStationType.Invalid;
            try { value = (LevelEditorStub.PseudoPrefabCookingUtensilStub.CookingStationType)Enum.Parse(enumType, names[i], true); }
            catch { }
            p.GetArrayElementAtIndex(i).enumValueIndex = (int)value;
        }
    }

    private static void SetObject(SerializedObject so, string field, UnityEngine.Object value)
    {
        var p = so.FindProperty(field);
        if (p != null) p.objectReferenceValue = value;
    }

    private static void SetInt(SerializedObject so, string field, int value)
    {
        var p = so.FindProperty(field);
        if (p != null) p.intValue = value;
    }

    private static void SetEnum(SerializedObject so, string field, string enumTypeName, string value)
    {
        var p = so.FindProperty(field);
        if (p == null) return;
        var t = FindType(enumTypeName);
        if (t == null) return;
        try { p.enumValueIndex = (int)Enum.Parse(t, value, true); }
        catch { }
    }

    private static Type FindType(string name)
    {
        var t = Type.GetType(name);
        if (t != null) return t;
        var asms = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < asms.Length; i++)
        {
            t = asms[i].GetType(name);
            if (t != null) return t;
        }
        return null;
    }

    private static T LoadAsset<T>(string guid) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(guid))
            return null;
        var path = AssetDatabase.GUIDToAssetPath(guid);
        return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
    }

    private static PlatingStepData FindPlatingStep(string id)
    {
        var roots = new[]
        {
            "Assets/common01/food/PlatingSteps",
            "Assets/common02/food/PlatingSteps",
            "Assets/common03/food/PlatingSteps",
            "Assets/common04/_supplies/PlatingStepData"
        };
        for (int i = 0; i < roots.Length; i++)
        {
            var guids = AssetDatabase.FindAssets("t:PseudoPrefabSO", new[] { roots[i] });
            for (int j = 0; j < guids.Length; j++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[j]);
                if (Path.GetFileNameWithoutExtension(path) != id)
                    continue;
                var so = AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(path);
                if (so == null) continue;
                var direct = so.asset as PlatingStepData;
                if (direct != null) return direct;
                if (!string.IsNullOrEmpty(so.bundleName) && !string.IsNullOrEmpty(so.assetPath))
                {
                    try
                    {
                        var bundle = LevelEditor.PseudoPrefabManager.GetAssetBundle(so.bundleName);
                        if (bundle != null)
                            return bundle.LoadAsset<PlatingStepData>(so.assetPath);
                    }
                    catch { }
                }
            }
        }
        return null;
    }

    private static void ApplyNodeIcon(IngredientOrderNode node, CustomContentConfigSO.Entry entry)
    {
        if (node == null)
            return;
        var sprite = LoadIconSprite(entry.prefabPath);
        if (sprite != null)
            node.m_iconSprite = sprite;
    }

    private static Sprite LoadIconSprite(string prefabPath)
    {
        var dir = Path.GetDirectoryName(prefabPath).Replace("\\", "/");
        var id = Path.GetFileNameWithoutExtension(prefabPath);
        var png = dir + "/" + id + "_Icon.png";
        return AssetDatabase.LoadAssetAtPath<Sprite>(png);
    }

    private static void SaveIcon(string setName, CustomContentConfigSO.Entry entry, CustomContentEditDto dto)
    {
        if (entry == null || string.IsNullOrEmpty(dto.iconBase64))
            return;
        var dir = Path.GetDirectoryName(entry.prefabPath).Replace("\\", "/");
        var id = Path.GetFileNameWithoutExtension(entry.prefabPath);
        var png = dir + "/" + id + "_Icon.png";
        try
        {
            var bytes = Convert.FromBase64String(dto.iconBase64);
            File.WriteAllBytes(AbsPath(png), bytes);
            AssetDatabase.ImportAsset(png, ImportAssetOptions.ForceUpdate);
            var imp = AssetImporter.GetAtPath(png) as TextureImporter;
            if (imp != null)
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.sRGBTexture = true;
                imp.alphaIsTransparency = true;
                imp.SaveAndReimport();
            }
            entry.iconPath = png;
            SetBundle(png, setName + "/" + ContentDir);
            var node = AssetDatabase.LoadAssetAtPath<IngredientOrderNode>(entry.nodePath);
            if (node != null)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(png);
                node.m_iconSprite = sprite;
                if (sprite != null)
                {
                    node.m_crateLid = new SubTexture2D
                    {
                        m_atlasTexture = sprite.texture,
                        m_subRect = sprite.textureRect
                    };
                }
                EditorUtility.SetDirty(node);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[CustomContent] 图标写入失败: " + ex.Message);
        }
    }

    private static void SaveModel(string setName, CustomContentConfigSO.Entry entry, CustomContentEditDto dto)
    {
        if (entry == null || dto.modelFiles == null || dto.modelFiles.Length == 0)
            return;
        var itemDir = Path.GetDirectoryName(entry.prefabPath).Replace("\\", "/");
        var id = Path.GetFileNameWithoutExtension(entry.prefabPath);
        var modelsDir = itemDir + "/models";
        if (!AssetDatabase.IsValidFolder(modelsDir))
            AssetDatabase.CreateFolder(itemDir, "models");
        try
        {
            string mainModel = null;
            for (int i = 0; i < dto.modelFiles.Length; i++)
            {
                var f = dto.modelFiles[i];
                if (f == null || string.IsNullOrEmpty(f.fileName) || string.IsNullOrEmpty(f.base64))
                    continue;
                var name = SanitizeFileName(f.fileName);
                if (string.IsNullOrEmpty(name)) continue;
                var path = modelsDir + "/" + name;
                File.WriteAllBytes(AbsPath(path), Convert.FromBase64String(f.base64));
                SetBundle(path, setName + "/" + ContentDir);
                if (mainModel == null && (name.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)
                    || name.EndsWith(".obj", StringComparison.OrdinalIgnoreCase)))
                    mainModel = path;
            }
            AssetDatabase.Refresh();
            if (Directory.Exists(AbsPath(modelsDir)))
            {
                foreach (var f in Directory.GetFiles(AbsPath(modelsDir)))
                {
                    if (f.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                        continue;
                    var n = Path.GetFileName(f);
                    SetBundle(modelsDir + "/" + n, setName + "/" + ContentDir);
                }
            }
            if (string.IsNullOrEmpty(mainModel))
                return;
            var imported = AssetDatabase.LoadAssetAtPath<GameObject>(mainModel);
            if (imported == null)
                return;
            var modelPrefabPath = modelsDir + "/" + id + "_Model.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(modelPrefabPath) != null)
                AssetDatabase.DeleteAsset(modelPrefabPath);
            var root = new GameObject(id + "_Model");
            try
            {
                var model = UnityEngine.Object.Instantiate(imported);
                model.name = imported.name;
                model.transform.SetParent(root.transform, false);
                PrefabUtility.CreatePrefab(modelPrefabPath, root);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
            SetBundle(modelPrefabPath, setName + "/" + ContentDir);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(entry.prefabPath);
            var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelPrefabPath);
            if (prefab == null || modelPrefab == null)
                return;
            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null) return;
            try
            {
                var existing = instance.transform.Find("WebCustomModel");
                if (existing != null)
                    UnityEngine.Object.DestroyImmediate(existing.gameObject);
                var renderers = instance.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                    renderers[i].enabled = false;
                var model = PrefabUtility.InstantiatePrefab(modelPrefab) as GameObject;
                model.name = "WebCustomModel";
                model.transform.SetParent(instance.transform, false);
                model.transform.localPosition = new Vector3(dto.modelPositionX, dto.modelPositionY, dto.modelPositionZ);
                model.transform.localEulerAngles = new Vector3(dto.modelRotationX, dto.modelRotationY, dto.modelRotationZ);
                if (dto.modelScale > 0f)
                    model.transform.localScale = Vector3.one * dto.modelScale;
                if (model.transform.childCount > 0)
                    model.transform.GetChild(0).localPosition =
                        new Vector3(dto.modelPivotX, dto.modelPivotY, dto.modelPivotZ);
                PrefabUtility.ReplacePrefab(instance, prefab, ReplacePrefabOptions.ReplaceNameBased);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[CustomContent] 模型写入失败: " + ex.Message);
        }
    }

    private static string Sanitize(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < raw.Length; i++)
        {
            var c = raw[i];
            if (char.IsLetterOrDigit(c) || c == '_') sb.Append(c);
        }
        return sb.ToString();
    }

    private static string SanitizeFileName(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        var name = Path.GetFileName(raw);
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.') sb.Append(c);
        }
        return sb.ToString();
    }

    private static string SetNameFromPath(string assetPath)
    {
        var m = System.Text.RegularExpressions.Regex.Match(assetPath.Replace("\\", "/"),
            @"/LevelSets/([^/]+)/" + ContentDir + "/");
        return m.Success ? m.Groups[1].Value : "";
    }

    private static PseudoPrefabSO FindStandaloneSo(string prefabPath)
    {
        var dir = Path.GetDirectoryName(prefabPath).Replace("\\", "/");
        var id = Path.GetFileNameWithoutExtension(prefabPath);
        return AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(dir + "/" + id + "_SO.asset");
    }

    private static bool BundleFileExists(string bundleName)
    {
        return LayoutEditorCatalogApi.BundleFileExists(bundleName);
    }

    private static void SetBundle(string assetPath, string bundleName)
    {
        var imp = AssetImporter.GetAtPath(assetPath);
        if (imp == null) return;
        if (imp.assetBundleName != bundleName)
        {
            imp.assetBundleName = bundleName;
            imp.SaveAndReimport();
        }
    }

    private static LayoutFootprint FootprintOf(CustomContentConfigSO.Entry entry)
    {
        if (entry == null)
            return new LayoutFootprint { cellsX = 1, cellsZ = 1 };
        if (entry.kind == "utensil" && entry.templateId == "utensil_hotpot")
            return new LayoutFootprint { cellsX = 2, cellsZ = 2 };
        if (entry.kind == "utensil" || entry.kind == "tray")
            return new LayoutFootprint { cellsX = 0.9f, cellsZ = 0.9f };
        if (entry.kind == "sink")
            return new LayoutFootprint { cellsX = 2, cellsZ = 1 };
        return new LayoutFootprint { cellsX = 1, cellsZ = 1 };
    }

    private static string StationLabelZh(string name)
    {
        switch (name)
        {
            case "Hob": return "灶台（Hob）";
            case "Oven": return "烤箱/烤盘（Oven）";
            case "DeepFatFryer": return "油炸锅（DeepFatFryer）";
            case "FirePit": return "篝火/煎盘（FirePit）";
            case "Flamethrower": return "喷火器（Flamethrower）";
            case "Barbeque": return "烧烤架/烤串（Barbeque）";
            case "Invalid_FloorBurner": return "火锅（FloorBurner）";
            default: return name;
        }
    }
}
