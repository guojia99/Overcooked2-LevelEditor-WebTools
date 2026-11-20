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
///  - 依赖 common04 的 supplies；common04 仅作编辑器克隆模板，不随依赖包导出。
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
    private const string CookingUI = "Assets/common04/_supplies/CookingUI.asset";

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
            }
            if (FindStandaloneSo(e.prefabPath) == null)
                warnings.Add("PseudoPrefabSO 缺失");
            if (!string.IsNullOrEmpty(e.iconPath) && !File.Exists(AbsPath(e.iconPath)))
                warnings.Add("图标文件缺失");
            // Local assets inherit info; creation/preview does not require a built bundle.
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
                updatedAt = e.updatedAt,
                bundleName = setName + "/info_" + setName,
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
        ReadLookupSettings(prefab, entry, dto);

        var splitter = prefab.GetComponent("IngredientSplitter");
        if (splitter != null)
        {
            LayoutEditorIngredientSplitterMigration.TryMigrate(splitter, true);
            var so = new SerializedObject(splitter);
            var cap = so.FindProperty("capacity");
            if (cap != null) dto.capacity = cap.intValue;
            var split = so.FindProperty("splitNum");
            if (split != null) dto.splitNum = split.intValue;
            var input = so.FindProperty("inputRecipe");
            if (input != null) dto.inputRecipeGuid = GuidOf(input.objectReferenceValue);
            // 2026-11 上游字段改名：outputPrefab(GameObject) -> outputPrefabSO(PseudoPrefabSO)。
            // 对前端保持 outputPrefabGuid 的 wire 名称，值改为 PseudoPrefabSO 的 guid。
            var output = so.FindProperty("outputPrefabSO") ?? so.FindProperty("outputPrefab");
            if (output != null) dto.outputPrefabGuid = GuidOf(output.objectReferenceValue);
            var audio = so.FindProperty("splitAudioTag");
            if (audio != null && audio.enumNames != null && audio.enumValueIndex >= 0 && audio.enumValueIndex < audio.enumNames.Length)
                dto.splitAudioTag = audio.enumNames[audio.enumValueIndex];
        }

        // 模型变换回读：表单编辑时回显已保存的 scale/position/rotation/pivot。
        var webModel = prefab.transform.Find("WebCustomModel");
        if (webModel != null)
        {
            dto.hasModel = true;
            dto.modelPositionX = webModel.localPosition.x;
            dto.modelPositionY = webModel.localPosition.y;
            dto.modelPositionZ = webModel.localPosition.z;
            dto.modelRotationX = webModel.localEulerAngles.x;
            dto.modelRotationY = webModel.localEulerAngles.y;
            dto.modelRotationZ = webModel.localEulerAngles.z;
            dto.modelScale = webModel.localScale.x;
            if (webModel.childCount > 0)
            {
                var pivot = webModel.GetChild(0).localPosition;
                dto.modelPivotX = pivot.x;
                dto.modelPivotY = pivot.y;
                dto.modelPivotZ = pivot.z;
            }
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

    private static void ReadLookupSettings(GameObject prefab, CustomContentConfigSO.Entry entry, CustomContentItemDto dto)
    {
        var trayType = FindType("SetupCustomTrayStub");
        if (trayType != null)
        {
            var trayStub = prefab.GetComponent(trayType);
            var slotsField = trayType.GetField("slots", BindingFlags.Public | BindingFlags.Instance);
            var slots = trayStub != null && slotsField != null ? slotsField.GetValue(trayStub) as Array : null;
            if (slots != null)
            {
                dto.traySlots = new CustomContentLookupSlotDto[slots.Length];
                for (int i = 0; i < slots.Length; i++)
                    dto.traySlots[i] = new CustomContentLookupSlotDto { rows = ReadLookupRows(slots.GetValue(i) as OrderToPrefabListSO) };
                if (slots.Length > 0)
                    dto.traySlotGuid = GuidOf(slots.GetValue(0) as UnityEngine.Object);
            }
        }

        var prep = prefab.GetComponent<PreparationContainer>();
        if (prep == null)
            return;
        var lookup = prep.m_containerRestrictions;
        dto.lookupGuid = GuidOf(lookup);
        dto.lookupRows = ReadLookupRows(lookup);
        var cosmetics = prep.m_cosmeticsPrefab;
        dto.cosmeticsPrefabGuid = GuidOf(cosmetics);
        var cosmeticsLookup = cosmetics != null ? cosmetics.GetComponent<BurgerBunCosmeticDecisions>() : null;
        if (cosmeticsLookup != null)
        {
            var field = typeof(BurgerBunCosmeticDecisions).GetField("m_prefabLookup", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
                dto.cosmeticsLookupGuid = GuidOf(field.GetValue(cosmeticsLookup) as OrderToPrefabLookup);
        }
    }

    private static CustomContentLookupRowDto[] ReadLookupRows(UnityEngine.Object asset)
    {
        var list = asset as OrderToPrefabListSO;
        if (list != null)
        {
            var rows = new List<CustomContentLookupRowDto>();
            if (list.recipes != null)
            {
                for (int i = 0; i < list.recipes.Length; i++)
                {
                    var row = new CustomContentLookupRowDto { recipeGuid = GuidOf(list.recipes[i]) };
                    row.readOnly = !(list.recipes[i] is CustomRecipeSO) && !(list.recipes[i] is PseudoPrefabSO);
                    if (row.readOnly) row.recipeGuid = "";
                    if (list.models != null && i < list.models.Length) row.modelGuid = GuidOf(list.models[i]);
                    if (list.modelSOs != null && i < list.modelSOs.Length) row.modelSoGuid = GuidOf(list.modelSOs[i]);
                    row.amountAllowed = 1;
                    rows.Add(row);
                }
            }
            return rows.ToArray();
        }

        var lookup = asset as OrderToPrefabLookup;
        if (lookup == null)
            return new CustomContentLookupRowDto[0];
        var field = typeof(OrderToPrefabLookup).GetField("m_lookupArray", BindingFlags.NonPublic | BindingFlags.Instance);
        var entries = field != null ? field.GetValue(lookup) as OrderToPrefabLookup.ContentPrefabLookup[] : null;
        if (entries == null)
            return new CustomContentLookupRowDto[0];
        var result = new List<CustomContentLookupRowDto>();
        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i] == null) continue;
            var restricted = new List<string>();
            if (entries[i].m_restrictedContent != null)
                for (int j = 0; j < entries[i].m_restrictedContent.Length; j++)
                    AddGuid(restricted, entries[i].m_restrictedContent[j]);
            result.Add(new CustomContentLookupRowDto
            {
                readOnly = string.IsNullOrEmpty(LookupRecipeGuid(entries[i].m_content)) || restricted.Count != (entries[i].m_restrictedContent != null ? entries[i].m_restrictedContent.Length : 0),
                recipeGuid = LookupRecipeGuid(entries[i].m_content),
                modelGuid = GuidOf(entries[i].m_prefab),
                amountAllowed = entries[i].m_amountAllowed,
                restrictedRecipeGuids = restricted.ToArray()
            });
        }
        return result.ToArray();
    }

    private static void AddGuid(List<string> guids, UnityEngine.Object obj)
    {
        var node = obj as OrderDefinitionNode;
        var guid = node != null ? LookupRecipeGuid(node) : GuidOf(obj);
        if (!string.IsNullOrEmpty(guid)) guids.Add(guid);
    }

    private static string LookupRecipeGuid(OrderDefinitionNode node)
    {
        const string prefix = "lookup_recipe_";
        if (node != null && node.name.StartsWith(prefix, StringComparison.Ordinal)
            && node.name.Length >= prefix.Length + 32)
        {
            var guid = node.name.Substring(prefix.Length, 32);
            var source = LoadAsset<ScriptableObject>(guid);
            if (source is CustomRecipeSO || source is PseudoPrefabSO) return guid;
        }
        return "";
    }

    private static string LookupAssetPath(CustomContentConfigSO.Entry entry)
    {
        return Path.GetDirectoryName(entry.prefabPath).Replace("\\", "/") + "/" + entry.id + "_Lookup.asset";
    }

    private static string CosmeticsAssetPath(CustomContentConfigSO.Entry entry)
    {
        return Path.GetDirectoryName(entry.prefabPath).Replace("\\", "/") + "/" + entry.id + "_Cosmetics.prefab";
    }

    private sealed class CustomContentLookupPlan
    {
        public OrderToPrefabListSO[] trayLists;
        public string[] traySourcePaths;
        public string[] trayAssetPaths;
        public List<CustomContentLookupRowDto[]> trayRows;
        public OrderToPrefabLookup prepLookup;
        public OrderToPrefabLookup.ContentPrefabLookup[] prepRows;
        public GameObject cosmeticsPrefab;
        public bool writeTray;
        public bool writePrep;
        public Dictionary<string, OrderDefinitionNode> nodes;
    }

    private static string ValidateLookupSettings(CustomContentEditDto dto, GameObject prefab,
        CustomContentConfigSO.Entry entry, out CustomContentLookupPlan plan)
    {
        plan = null;
        var wantsTray = dto.traySlots != null;
        var wantsPrep = dto.lookupRows != null;
        if (!wantsTray && !wantsPrep)
            return null;
        plan = new CustomContentLookupPlan();
        plan.nodes = new Dictionary<string, OrderDefinitionNode>();
        try
        {
            if (wantsPrep)
            {
                var err = ValidateLookupRows(dto.lookupRows, plan.nodes);
                if (err != null) return err;
            }
        }
        catch (Exception ex) { return "菜谱节点解析失败：" + ex.Message; }

        if (wantsTray)
        {
            if (dto.traySlots.Length == 0) return "traySlots 不能为空。";
            if (entry.kind != "tray" && !(entry.kind == "utensil" && entry.templateId == "utensil_tray"))
                return "traySlots 仅支持 tray 或 utensil_tray。";
            var trayType = FindType("SetupCustomTrayStub");
            var trayStub = trayType != null ? prefab.GetComponent(trayType) : null;
            var slotsField = trayType != null ? trayType.GetField("slots", BindingFlags.Public | BindingFlags.Instance) : null;
            var oldSlots = trayStub != null && slotsField != null ? slotsField.GetValue(trayStub) as Array : null;
            if (oldSlots == null || oldSlots.Length != dto.traySlots.Length)
                return "traySlots 数量必须等于模板现有槽位数。";
            plan.trayLists = new OrderToPrefabListSO[oldSlots.Length];
            plan.traySourcePaths = new string[oldSlots.Length];
            plan.trayAssetPaths = new string[oldSlots.Length];
            plan.trayRows = new List<CustomContentLookupRowDto[]>();
            for (int i = 0; i < oldSlots.Length; i++)
            {
                var oldList = oldSlots.GetValue(i) as OrderToPrefabListSO;
                if (oldList == null)
                    return "模板第 " + i + " 个槽位没有 OrderToPrefabListSO，拒绝更改槽位结构。";
                var sourcePath = AssetDatabase.GetAssetPath(oldList);
                if (string.IsNullOrEmpty(sourcePath)) return "模板槽位资产路径缺失。";
                var ownPath = Path.GetDirectoryName(entry.prefabPath).Replace("\\", "/") + "/" + entry.id + "_Slot_" + i + ".asset";
                var rows = dto.traySlots[i] != null ? dto.traySlots[i].rows : null;
                if (rows == null || rows.Length == 0)
                    return "traySlots[" + i + "].rows 不能为空。";
                for (int j = 0; j < rows.Length; j++)
                {
                    var err = ValidateLookupRow(rows[j], false);
                    if (err != null) return "traySlots[" + i + "].rows[" + j + "]: " + err;
                    if (rows[j].amountAllowed != 1 || rows[j].restrictedRecipeGuids != null && rows[j].restrictedRecipeGuids.Length > 0)
                        return "tray 行仅支持 amountAllowed=1 且不支持 restrictedRecipeGuids。";
                }
                plan.traySourcePaths[i] = sourcePath;
                plan.trayAssetPaths[i] = ownPath;
                plan.trayRows.Add(rows);
            }
            plan.writeTray = true;
        }

        if (wantsPrep)
        {
            if (entry.kind != "prep" && !(entry.kind == "ingredient" && entry.templateId == "ingredient_preparation"))
                return "lookupRows 仅支持 prep 或 ingredient_preparation。";
            var prep = prefab.GetComponent<PreparationContainer>();
            if (prep == null)
                return "目标 prefab 不含 PreparationContainer。";
            if (dto.lookupRows.Length == 0)
                return "lookupRows 不能为空。";
            var nativeCosmetics = prep.m_cosmeticsPrefab;
            if (nativeCosmetics == null)
                return "PreparationContainer 缺少模板 cosmetics prefab。";
            var cosmeticComponent = nativeCosmetics.GetComponent<BurgerBunCosmeticDecisions>();
            if (cosmeticComponent == null)
                return "模板 cosmetics 不含 BurgerBunCosmeticDecisions，无法安全绑定 lookup。";
            var cosmeticLookupField = typeof(BurgerBunCosmeticDecisions).GetField("m_prefabLookup", BindingFlags.NonPublic | BindingFlags.Instance);
            if (cosmeticLookupField == null)
                return "BurgerBunCosmeticDecisions.m_prefabLookup 不存在。";
            plan.prepRows = new OrderToPrefabLookup.ContentPrefabLookup[dto.lookupRows.Length];
            for (int i = 0; i < dto.lookupRows.Length; i++)
            {
                var err = ValidateLookupRow(dto.lookupRows[i], true);
                if (err != null) return "lookupRows[" + i + "]: " + err;
                var recipe = LoadAsset<ScriptableObject>(dto.lookupRows[i].recipeGuid);
                var model = LoadAsset<GameObject>(dto.lookupRows[i].modelGuid);
                if (model == null && !string.IsNullOrEmpty(dto.lookupRows[i].modelSoGuid))
                {
                    var modelSo = LoadAsset<PseudoPrefabSO>(dto.lookupRows[i].modelSoGuid);
                    model = ResolveLookupModel(modelSo);
                }
                var node = recipe != null ? plan.nodes[dto.lookupRows[i].recipeGuid] : null;
                if (!IsLookupPersistent(model)) return "无法加载持久化 prep 行模型：" + i;
                if (node == null) return "无法为菜谱生成 OrderDefinitionNode：" + dto.lookupRows[i].recipeGuid;
                plan.prepRows[i] = new OrderToPrefabLookup.ContentPrefabLookup
                {
                    m_content = node,
                    m_prefab = model,
                    m_amountAllowed = dto.lookupRows[i].amountAllowed,
                    m_restrictedContent = ResolveRestrictedNodes(dto.lookupRows[i].restrictedRecipeGuids, plan.nodes)
                };
            }
            var lookupPath = LookupAssetPath(entry);
            plan.prepLookup = AssetDatabase.LoadAssetAtPath<OrderToPrefabLookup>(lookupPath);
            if (plan.prepLookup == null)
                plan.prepLookup = ScriptableObject.CreateInstance<OrderToPrefabLookup>();
            plan.cosmeticsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CosmeticsAssetPath(entry));
            if (plan.cosmeticsPrefab != null && plan.cosmeticsPrefab.GetComponent<BurgerBunCosmeticDecisions>() == null)
                return "自有 cosmetics prefab 加载或类型检查失败。";
            plan.writePrep = true;
        }
        return null;
    }

    private static string ValidateLookupRow(CustomContentLookupRowDto row, bool prep)
    {
        if (row != null && row.readOnly) return "原生 lookup 行只读，必须显式选择可编辑菜谱来源。";
        if (row == null || string.IsNullOrEmpty(row.recipeGuid)) return "recipeGuid 必填。";
        var recipe = LoadAsset<ScriptableObject>(row.recipeGuid);
        if (recipe == null || !(recipe is CustomRecipeSO) && !(recipe is PseudoPrefabSO)) return "recipeGuid 必须解析到 CustomRecipeSO 或 PseudoPrefabSO。";
        if (row.amountAllowed < 1) return "amountAllowed 必须大于 0。";
        if (prep && string.IsNullOrEmpty(row.modelGuid) && string.IsNullOrEmpty(row.modelSoGuid)) return "prep 行必须提供 modelGuid 或 modelSoGuid。";
        if (!string.IsNullOrEmpty(row.modelGuid) && !IsLookupPersistent(LoadAsset<GameObject>(row.modelGuid))) return "modelGuid 无法解析到持久化 prefab。";
        if (!string.IsNullOrEmpty(row.modelSoGuid) && LoadAsset<PseudoPrefabSO>(row.modelSoGuid) == null) return "modelSoGuid 无法解析到 PseudoPrefabSO。";
        if (!string.IsNullOrEmpty(row.modelSoGuid))
        {
            var modelSo = LoadAsset<PseudoPrefabSO>(row.modelSoGuid);
            var model = ResolveLookupModel(modelSo);
            if (!IsLookupPersistent(model)) return "modelSo 无法解析到持久化 prefab。";
        }
        if (row.restrictedRecipeGuids != null)
            for (int i = 0; i < row.restrictedRecipeGuids.Length; i++)
                if (LoadAsset<ScriptableObject>(row.restrictedRecipeGuids[i]) == null) return "restrictedRecipeGuids 中含无效 GUID。";
        return null;
    }

    private static bool IsLookupPersistent(UnityEngine.Object asset)
    {
        return asset != null && EditorUtility.IsPersistent(asset) && AssetDatabase.GetAssetPath(asset).StartsWith("Assets/", StringComparison.Ordinal)
            && File.Exists(AssetDatabase.GetAssetPath(asset));
    }

    private static GameObject ResolveLookupModel(PseudoPrefabSO modelSo)
    {
        if (modelSo == null) return null;
        var model = modelSo.asset as GameObject;
        if (IsLookupPersistent(model)) return model;
        var path = (modelSo.assetPath ?? "").Replace("\\", "/");
        model = path.StartsWith("Assets/", StringComparison.Ordinal) ? AssetDatabase.LoadAssetAtPath<GameObject>(path) : null;
        return IsLookupPersistent(model) ? model : null;
    }

    private static void AuditLookupNode(ScriptableObject node, HashSet<ScriptableObject> visited)
    {
        if (node == null || !visited.Add(node)) return;
        var serialized = new SerializedObject(node);
        var property = serialized.GetIterator();
        while (property.Next(true))
        {
            if (property.propertyType != SerializedPropertyType.ObjectReference || property.propertyPath == "m_Script") continue;
            var reference = property.objectReferenceValue;
            if (reference == null) continue;
            var child = reference as OrderDefinitionNode;
            if (child != null) { AuditLookupNode(child, visited); continue; }
            if (reference is CookingStepData || reference is PlatingStepData)
            {
                AuditLookupNode(reference as ScriptableObject, visited);
                continue;
            }
            if (!IsLookupPersistent(reference))
                throw new InvalidOperationException("节点引用无法持久化：" + node.name + "." + property.propertyPath + " (" + reference.name + ")");
        }
    }

    private static string ValidateLookupRows(CustomContentLookupRowDto[] rows, Dictionary<string, OrderDefinitionNode> nodes)
    {
        if (rows == null) return "rows 必填。";
        for (int i = 0; i < rows.Length; i++)
        {
            var err = ValidateLookupRow(rows[i], false);
            if (err != null) return "rows[" + i + "]: " + err;
            var guids = new List<string> { rows[i].recipeGuid };
            if (rows[i].restrictedRecipeGuids != null) guids.AddRange(rows[i].restrictedRecipeGuids);
            foreach (var guid in guids)
            {
                if (nodes.ContainsKey(guid)) continue;
                var recipe = LoadAsset<ScriptableObject>(guid);
                if (!(recipe is CustomRecipeSO) && !(recipe is PseudoPrefabSO))
                    return "不支持的菜谱引用：" + guid;
                OrderDefinitionNode node;
                var hydrated = new Dictionary<PseudoPrefabSO, UnityEngine.Object>();
                try
                {
                    HydrateLookupRecipe(recipe, hydrated, new HashSet<ScriptableObject>());
                    node = LevelEditor.RecipeHelper.GetOrderDefinitionNode(recipe);
                }
                finally
                {
                    foreach (var pair in hydrated) pair.Key.asset = pair.Value;
                }
                if (node == null) return "无法解析菜谱节点：" + guid;
                AuditLookupNode(node, new HashSet<ScriptableObject>());
                if (AssetDatabase.Contains(node)) node = UnityEngine.Object.Instantiate(node) as OrderDefinitionNode;
                nodes.Add(guid, node);
            }
        }
        return null;
    }

    private static void HydrateLookupRecipe(ScriptableObject recipe, Dictionary<PseudoPrefabSO, UnityEngine.Object> hydrated,
        HashSet<ScriptableObject> visited)
    {
        if (recipe == null || !visited.Add(recipe)) return;
        var wrapper = recipe as PseudoPrefabSO;
        if (wrapper != null)
        {
            if (wrapper.asset != null) return;
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>((wrapper.assetPath ?? "").Replace("\\", "/"));
            if (asset == null)
            {
                AssetBundle bundle = null;
                foreach (var loaded in AssetBundle.GetAllLoadedAssetBundles())
                    if (loaded.name == wrapper.bundleName) { bundle = loaded; break; }
                if (bundle == null)
                    bundle = AssetBundle.LoadFromFile(Path.Combine(Path.Combine(Application.streamingAssetsPath, "Windows"), wrapper.bundleName));
                if (bundle != null) asset = bundle.LoadAsset<UnityEngine.Object>(wrapper.assetPath.Replace("\\", "/"));
            }
            if (asset == null) throw new InvalidOperationException("无法加载菜谱依赖：" + wrapper.assetPath);
            hydrated.Add(wrapper, wrapper.asset);
            wrapper.asset = asset;
            return;
        }
        // Walk serialized SO references, including optional recipe implementations.
        var serialized = new SerializedObject(recipe);
        var property = serialized.GetIterator();
        while (property.Next(true))
            if (property.propertyType == SerializedPropertyType.ObjectReference)
                HydrateLookupRecipe(property.objectReferenceValue as ScriptableObject, hydrated, visited);
    }

    private static OrderDefinitionNode[] ResolveRestrictedNodes(string[] recipeGuids, Dictionary<string, OrderDefinitionNode> resolved)
    {
        if (recipeGuids == null || recipeGuids.Length == 0)
            return new OrderDefinitionNode[0];
        var nodes = new List<OrderDefinitionNode>();
        for (int i = 0; i < recipeGuids.Length; i++)
        {
            nodes.Add(resolved[recipeGuids[i]]);
        }
        return nodes.ToArray();
    }

    private static void ApplyLookupBindings(GameObject instance, CustomContentConfigSO.Entry entry, CustomContentLookupPlan plan)
    {
        if (plan == null) return;
        if (plan.writeTray)
        {
            var trayType = FindType("SetupCustomTrayStub");
            var stub = trayType != null ? instance.GetComponent(trayType) : null;
            var field = trayType != null ? trayType.GetField("slots", BindingFlags.Public | BindingFlags.Instance) : null;
            if (stub != null && field != null) field.SetValue(stub, plan.trayLists);
        }
        if (plan.writePrep)
        {
            var prep = instance.GetComponent<PreparationContainer>();
            if (prep != null) prep.m_containerRestrictions = plan.prepLookup;
            if (prep != null) prep.m_cosmeticsPrefab = plan.cosmeticsPrefab;
        }
    }

    private static void PersistLookupAssets(CustomContentConfigSO.Entry entry, CustomContentLookupPlan plan, string setName)
    {
        if (plan == null) return;
        if (plan.writeTray)
        {
            for (int i = 0; i < plan.trayLists.Length; i++)
            {
                var list = AssetDatabase.LoadAssetAtPath<OrderToPrefabListSO>(plan.trayAssetPaths[i]);
                if (list == null)
                {
                    if (!AssetDatabase.CopyAsset(plan.traySourcePaths[i], plan.trayAssetPaths[i]))
                        throw new InvalidOperationException("创建自有槽位列表失败。");
                    list = AssetDatabase.LoadAssetAtPath<OrderToPrefabListSO>(plan.trayAssetPaths[i]);
                }
                if (list == null) throw new InvalidOperationException("自有槽位列表加载失败。");
                plan.trayLists[i] = list;
                var rows = plan.trayRows[i];
                var recipes = new ScriptableObject[rows.Length];
                var models = new GameObject[rows.Length];
                var modelSOs = new PseudoPrefabSO[rows.Length];
                for (int j = 0; j < rows.Length; j++)
                {
                    recipes[j] = LoadAsset<ScriptableObject>(rows[j].recipeGuid);
                    models[j] = LoadAsset<GameObject>(rows[j].modelGuid);
                    modelSOs[j] = LoadAsset<PseudoPrefabSO>(rows[j].modelSoGuid);
                }
                list.recipes = recipes;
                list.models = models;
                list.modelSOs = modelSOs;
                EditorUtility.SetDirty(list);
                InheritInfoBundle(AssetDatabase.GetAssetPath(list));
            }
        }
        if (!plan.writePrep) return;
        if (plan.cosmeticsPrefab == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(entry.prefabPath);
            var prep = prefab != null ? prefab.GetComponent<PreparationContainer>() : null;
            var source = prep != null ? prep.m_cosmeticsPrefab : null;
            var cosmeticsPath = CosmeticsAssetPath(entry);
            if (source == null || !AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source), cosmeticsPath))
                throw new InvalidOperationException("创建自有 cosmetics prefab 失败。");
            plan.cosmeticsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(cosmeticsPath);
            if (plan.cosmeticsPrefab == null) throw new InvalidOperationException("自有 cosmetics prefab 加载失败。");
        }
        var lookupPath = LookupAssetPath(entry);
        if (!AssetDatabase.Contains(plan.prepLookup))
            AssetDatabase.CreateAsset(plan.prepLookup, lookupPath);
        // Reuse named subassets so repeated edits retain local file IDs and the lookup GUID.
        var existingNodes = new Dictionary<string, OrderDefinitionNode>();
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(lookupPath))
        {
            var node = asset as OrderDefinitionNode;
            if (node != null) existingNodes[node.name] = node;
        }
        var persisted = new Dictionary<OrderDefinitionNode, OrderDefinitionNode>();
        var keys = new List<string>(plan.nodes.Keys);
        foreach (var key in keys)
            plan.nodes[key] = PersistNode(plan.nodes[key], plan.prepLookup, "lookup_recipe_" + key, existingNodes, persisted);
        for (int i = 0; i < plan.prepRows.Length; i++)
        {
            plan.prepRows[i].m_content = persisted[plan.prepRows[i].m_content];
            var restricted = plan.prepRows[i].m_restrictedContent;
            for (int j = 0; j < restricted.Length; j++) restricted[j] = persisted[restricted[j]];
        }
        plan.prepLookup.GetType().GetField("m_lookupArray", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(plan.prepLookup, plan.prepRows);
        EditorUtility.SetDirty(plan.prepLookup);
        AssetDatabase.SaveAssets();
        InheritInfoBundle(lookupPath);

        var cosmeticPath = CosmeticsAssetPath(entry);
        var cosmeticInstance = PrefabUtility.InstantiatePrefab(plan.cosmeticsPrefab) as GameObject;
        if (cosmeticInstance == null) throw new InvalidOperationException("实例化自有 cosmetics 失败。");
        if (cosmeticInstance != null)
        {
            try
            {
                var cosmetic = cosmeticInstance.GetComponent<BurgerBunCosmeticDecisions>();
                var field = typeof(BurgerBunCosmeticDecisions).GetField("m_prefabLookup", BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null) field.SetValue(cosmetic, plan.prepLookup);
                PrefabUtility.ReplacePrefab(cosmeticInstance, plan.cosmeticsPrefab, ReplacePrefabOptions.ReplaceNameBased);
            }
            finally { UnityEngine.Object.DestroyImmediate(cosmeticInstance); }
        }
        InheritInfoBundle(cosmeticPath);
    }

    private static OrderDefinitionNode PersistNode(OrderDefinitionNode node, OrderToPrefabLookup owner, string name,
        Dictionary<string, OrderDefinitionNode> existing, Dictionary<OrderDefinitionNode, OrderDefinitionNode> persisted)
    {
        if (node == null) return null;
        OrderDefinitionNode result;
        if (persisted.TryGetValue(node, out result)) return result;
        // Ingredient nodes already backed by assets remain shared and are never modified.
        if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(node))) { persisted[node] = node; return node; }
        if (!existing.TryGetValue(name, out result) || result.GetType() != node.GetType())
        {
            result = ScriptableObject.CreateInstance(node.GetType()) as OrderDefinitionNode;
            result.name = name;
            AssetDatabase.AddObjectToAsset(result, owner);
        }
        persisted[node] = result;
        var composite = node as CompositeOrderNode;
        if (composite != null)
        {
            if (composite.m_composition != null)
                for (int i = 0; i < composite.m_composition.Length; i++) composite.m_composition[i] = PersistNode(composite.m_composition[i], owner, name + "_composition_" + i, existing, persisted);
            if (composite.m_optional != null)
                for (int i = 0; i < composite.m_optional.Length; i++) composite.m_optional[i] = PersistNode(composite.m_optional[i], owner, name + "_optional_" + i, existing, persisted);
        }
        EditorUtility.CopySerialized(node, result);
        result.name = name;
        var cooked = result as CookedCompositeOrderNode;
        if (cooked != null && cooked.m_cookingStep != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(cooked.m_cookingStep)))
        {
            var stepName = "lookup_step_" + cooked.m_cookingStep.m_uID;
            CookingStepData step = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(owner)))
                if (asset is CookingStepData && asset.name == stepName
                    && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(asset as CookingStepData)))) { step = asset as CookingStepData; break; }
            if (step == null)
            {
                step = ScriptableObject.CreateInstance<CookingStepData>();
                step.name = stepName;
                AssetDatabase.AddObjectToAsset(step, owner);
            }
            CopyLookupStep(cooked.m_cookingStep, step);
            cooked.m_cookingStep = step;
        }
        if (result.m_platingStep != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(result.m_platingStep)))
        {
            var stepName = "lookup_plating_" + result.m_platingStep.m_uID;
            PlatingStepData step = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(owner)))
                if (asset is PlatingStepData && asset.name == stepName
                    && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(asset as PlatingStepData)))) { step = asset as PlatingStepData; break; }
            if (step == null)
            {
                step = ScriptableObject.CreateInstance<PlatingStepData>();
                step.name = stepName;
                AssetDatabase.AddObjectToAsset(step, owner);
            }
            CopyLookupStep(result.m_platingStep, step);
            result.m_platingStep = step;
        }
        EditorUtility.SetDirty(result);
        return result;
    }

    private static void CopyLookupStep(ScriptableObject source, ScriptableObject target)
    {
        var name = target.name;
        for (var type = source.GetType(); type != null && type != typeof(ScriptableObject); type = type.BaseType)
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                if (!field.IsInitOnly && !field.IsNotSerialized && (field.IsPublic || Attribute.IsDefined(field, typeof(SerializeField))))
                    field.SetValue(target, field.GetValue(source));
        target.name = name;
        var serialized = new SerializedObject(target);
        var property = serialized.GetIterator();
        while (property.Next(true))
        {
            if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
            if (property.propertyPath == "m_Script") continue;
            if (property.objectReferenceValue != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(property.objectReferenceValue)))
                property.objectReferenceValue = null;
        }
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
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
        var id = dto.id.Trim();
        if (string.IsNullOrEmpty(setName))
            return "关卡集标识非法。";
        if (!IsValidId(id))
            return "标识 id 只能包含英文字母、数字和下划线（不支持中文、连字符、空格）。";
        var allowedKinds = new[] { "ingredient", "counter", "utensil", "sink", "tray", "prep", "splitter" };
        if (Array.IndexOf(allowedKinds, dto.kind) < 0)
            return "未知自定义类型：" + dto.kind;
        var template = FindTemplate(dto.kind, dto.templateId);
        if (template == null)
            return "未找到模板：" + (dto.templateId ?? "(空)");
        var uidErr = ValidateOrderUidNotTaken(dto.orderUid, "");
        if (uidErr != null)
            return uidErr;
        // 显式指定「可加工下一形态」时校验引用可解析（前端联动创建切片食材后回填 GUID）。
        if (!string.IsNullOrEmpty(dto.nextPrefabGuid) && LoadAsset<GameObject>(dto.nextPrefabGuid) == null)
            return "可加工下一形态引用无效（GUID 未命中任何 prefab）。";

        if (!AssetDatabase.IsValidFolder(LevelSetsRoot + "/" + setName))
            return "关卡集不存在：" + setName;
        var root = Root(setName);
        var itemDir = root + "/" + dto.kind + "/" + id;
        if (AssetDatabase.IsValidFolder(itemDir))
            return "自定义内容已存在：" + dto.kind + "/" + id;
        // id 在关卡集内全局唯一（跨 kind）：GetCatalog/前端按裸 id 索引，跨类型同名会互相顶掉。
        var existingConfig = LoadConfig(setName);
        if (existingConfig != null && existingConfig.entries != null)
        {
            for (int i = 0; i < existingConfig.entries.Length; i++)
            {
                var e = existingConfig.entries[i];
                if (e != null && e.id == id)
                    return "标识 id 已被 " + e.kind + "/" + e.id + " 占用，同一关卡集内不能重名，请换一个 id。";
            }
        }

        var err = CreateAssets(dto, setName, id, template, itemDir);
        if (err != null)
        {
            RollbackCreate(setName, itemDir);
            return err;
        }
        return null;
    }

    /// <summary>实际写入流程；任何一步失败返回错误（由调用方回滚已创建的目录）。</summary>
    private static string CreateAssets(CustomContentEditDto dto, string setName, string id,
        CustomContentTemplateDto template, string itemDir)
    {
        EnsureConfig(setName);
        var root = Root(setName);
        InheritInfoBundle(root);
        if (!AssetDatabase.IsValidFolder(root))
            return "无法创建自定义内容目录：" + root;
        EnsureFolder(root, dto.kind);
        EnsureFolder(root + "/" + dto.kind, id);

        var prefabPath = itemDir + "/" + id + ".prefab";
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
        // 可加工食材：模板自带 nodePath 为空，必须补一个独立 OrderNode（订单/菜谱/图标/切割后的
        // 成品匹配都依赖它）。未显式指定「下一形态」时，同时克隆 unworkable 形态；WorkableItem 的
        // m_nextPrefab 指向新形态（否则切完仍回到模板番茄/汉堡）。前端已联动创建切片食材
        // （dto.nextPrefabGuid 非空）时只补 OrderNode、跳过自动克隆，m_nextPrefab 走显式关联。
        if (dto.kind == "ingredient" && template.id == "ingredient_workable")
        {
            nodePath = itemDir + "/" + id + "_Node.asset";
            if (!AssetDatabase.CopyAsset(NodeIngredient, nodePath))
                return "复制食材 OrderNode 失败。";
            if (string.IsNullOrEmpty(dto.nextPrefabGuid))
            {
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

        InheritInfoBundle(prefabPath);
        if (!string.IsNullOrEmpty(workableNextPrefabPath))
            InheritInfoBundle(workableNextPrefabPath);
        if (!string.IsNullOrEmpty(nodePath))
            InheritInfoBundle(nodePath);
        InheritInfoBundle(soPath);
        if (!string.IsNullOrEmpty(dto.iconBase64))
        {
            var iconErr = SaveIcon(setName, entry, dto);
            if (iconErr != null)
                return iconErr;
        }
        if (dto.modelFiles != null && dto.modelFiles.Length > 0)
        {
            var modelErr = SaveModel(setName, entry, dto);
            if (modelErr != null)
                return modelErr;
        }
        LayoutEditorLevelAdminApi.InvalidatePseudoPrefabSearchFolders();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return null;
    }

    /// <summary>创建中途失败时清理：删除已建条目目录并移除 Config 条目，避免孤儿资产。</summary>
    private static void RollbackCreate(string setName, string itemDir)
    {
        try
        {
            if (!string.IsNullOrEmpty(itemDir) && AssetDatabase.IsValidFolder(itemDir))
                AssetDatabase.DeleteAsset(itemDir);
            var config = LoadConfig(setName);
            if (config != null && config.entries != null)
            {
                var list = new List<CustomContentConfigSO.Entry>(config.entries);
                var changed = false;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var e = list[i];
                    if (e != null && !string.IsNullOrEmpty(e.prefabPath) && e.prefabPath.StartsWith(itemDir, StringComparison.Ordinal))
                    {
                        list.RemoveAt(i);
                        changed = true;
                    }
                }
                if (changed)
                {
                    config.entries = list.ToArray();
                    EditorUtility.SetDirty(config);
                }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[CustomContent] 创建回滚失败: " + ex.Message);
        }
    }

    /// <summary>手填 OrderNode uID 时校验全局唯一（原版/其它自定义节点都算占用）。</summary>
    private static string ValidateOrderUidNotTaken(int uid, string selfNodePath)
    {
        if (uid <= 0)
            return null;
        var guids = AssetDatabase.FindAssets("t:IngredientOrderNode");
        for (int i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (!string.IsNullOrEmpty(selfNodePath) && path == selfNodePath)
                continue;
            var node = AssetDatabase.LoadAssetAtPath<IngredientOrderNode>(path);
            if (node != null && node.m_uID == uid)
                return "OrderNode uID " + uid + " 已被占用（" + path + "）。请改用 0 自动分配。";
        }
        return null;
    }

    public static string Update(CustomContentEditDto dto)
    {
        if (dto == null || string.IsNullOrEmpty(dto.assetPath) || dto.assetPath.IndexOf("/" + ContentDir + "/", StringComparison.Ordinal) < 0)
            return "缺少或无效 assetPath。";
        if (!string.IsNullOrEmpty(dto.platingStepId) && FindPlatingStep(dto.platingStepId) == null)
            return "未找到装盘容器：" + dto.platingStepId;
        try
        {
            if (!string.IsNullOrEmpty(dto.iconBase64))
            {
                var icon = Convert.FromBase64String(dto.iconBase64);
                if (icon.Length < 8 || icon[0] != 137 || icon[1] != 80 || icon[2] != 78 || icon[3] != 71
                    || icon[4] != 13 || icon[5] != 10 || icon[6] != 26 || icon[7] != 10)
                    return "图标必须是 PNG 格式。";
            }
            if (dto.modelFiles != null && dto.modelFiles.Length > 0)
            {
                var hasModel = false;
                foreach (var file in dto.modelFiles)
                {
                    if (file == null || string.IsNullOrEmpty(file.fileName) || string.IsNullOrEmpty(file.base64)) continue;
                    Convert.FromBase64String(file.base64);
                    var extension = Path.GetExtension(file.fileName).ToLowerInvariant();
                    if (extension == ".fbx" || extension == ".obj") hasModel = true;
                }
                if (!hasModel) return "模型文件中未找到 .fbx / .obj 主模型。";
            }
        }
        catch (Exception ex) { return "上传数据预检失败：" + ex.Message; }
        if (dto.lookupRows == null && dto.traySlots == null) return UpdateInner(dto);
        var directory = Path.GetDirectoryName(dto.assetPath);
        if (!Directory.Exists(directory)) return "未找到资产目录。";
        var snapshot = new Dictionary<string, byte[]>();
        foreach (var path in Directory.GetFiles(directory, "*", SearchOption.AllDirectories)) snapshot[path] = File.ReadAllBytes(path);
        var configPath = Root(SetNameFromPath(dto.assetPath)) + "/Config.asset";
        if (File.Exists(configPath)) snapshot[configPath] = File.ReadAllBytes(configPath);
        string error;
        try { error = UpdateInner(dto); }
        catch (Exception ex) { error = "更新自定义内容失败：" + ex.Message; }
        if (error == null) return null;
        // A lookup update is local to this entry and Config; restore bytes and subasset IDs on any failure.
        try
        {
            AssetDatabase.SaveAssets();
            foreach (var path in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
                if (!snapshot.ContainsKey(path)) File.Delete(path);
            foreach (var pair in snapshot) File.WriteAllBytes(pair.Key, pair.Value);
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            foreach (var pair in snapshot)
                if (!pair.Key.EndsWith(".meta", StringComparison.Ordinal)) AssetDatabase.ImportAsset(pair.Key.Replace("\\", "/"), ImportAssetOptions.ForceUpdate);
        }
        catch (Exception ex) { return error + "；回滚失败：" + ex.Message; }
        return error;
    }

    private static string UpdateInner(CustomContentEditDto dto)
    {
        if (dto == null || string.IsNullOrEmpty(dto.assetPath))
            return "缺少 assetPath。";
        if (dto.assetPath.IndexOf("/" + ContentDir + "/", StringComparison.Ordinal) < 0)
            return "只允许更新 custom_content 内的资产。";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(dto.assetPath);
        if (prefab == null)
            return "未找到 prefab：" + dto.assetPath;
        var setName = SetNameFromPath(dto.assetPath);
        if (string.IsNullOrEmpty(setName))
            return "无法从路径解析关卡集：" + dto.assetPath;
        var config = EnsureConfig(setName);
        var entry = config != null ? FindEntry(config, dto.assetPath) : null;
        if (entry == null)
            return "Config 中找不到该自定义内容条目：" + dto.assetPath;
        if (!string.IsNullOrEmpty(dto.templateId) && !string.IsNullOrEmpty(entry.templateId)
            && dto.templateId != entry.templateId)
            return "模板在创建后不可更改（当前模板：" + entry.templateId + "）。";
        var uidErr = ValidateOrderUidNotTaken(dto.orderUid, entry.nodePath ?? "");
        if (uidErr != null)
            return uidErr;
        CustomContentLookupPlan lookupPlan;
        var lookupErr = ValidateLookupSettings(dto, prefab, entry, out lookupPlan);
        if (lookupErr != null)
            return lookupErr;
        // Loading recipe bundles can invalidate previously loaded editor asset handles.
        config = EnsureConfig(setName);
        entry = config != null ? FindEntry(config, dto.assetPath) : null;
        prefab = AssetDatabase.LoadAssetAtPath<GameObject>(dto.assetPath);
        if (entry == null || prefab == null) return "匹配表保存后无法重新加载自定义内容。";
        // 自愈：旧版「显式切片关联」创建的可加工食材漏建 OrderNode（nodePath 为空）——编辑保存时
        // 补建一个独立 OrderNode 并把 prefab 接线，避免只能删除重建。
        var healedNode = false;
        if (string.IsNullOrEmpty(entry.nodePath) && entry.kind == "ingredient"
            && entry.templateId == "ingredient_workable")
        {
            var healDir = Path.GetDirectoryName(entry.prefabPath).Replace("\\", "/");
            var healNodePath = healDir + "/" + entry.id + "_Node.asset";
            if (AssetDatabase.LoadAssetAtPath<IngredientOrderNode>(healNodePath) == null)
            {
                if (!AssetDatabase.CopyAsset(NodeIngredient, healNodePath))
                    return "补建 OrderNode 失败：" + healNodePath;
                InheritInfoBundle(healNodePath);
            }
            entry.nodePath = healNodePath;
            healedNode = true;
        }
        try
        {
            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
                return "实例化 prefab 失败。";
            try
            {
                ApplyCommonSettings(instance, dto);
                ApplyClearFields(instance, dto);
                if (healedNode) ApplyNode(instance, entry.nodePath, dto);
                PrefabUtility.ReplacePrefab(instance, prefab, ReplacePrefabOptions.ReplaceNameBased);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }

            if (!string.IsNullOrEmpty(dto.nameZh)) entry.nameZh = dto.nameZh;
            if (!string.IsNullOrEmpty(dto.nameEn)) entry.nameEn = dto.nameEn;
            entry.updatedAt = DateTime.UtcNow.ToString("o");
            if (!string.IsNullOrEmpty(entry.nodePath))
            {
                var node = AssetDatabase.LoadAssetAtPath<IngredientOrderNode>(entry.nodePath);
                if (node != null)
                {
                    if (dto.orderUid > 0) node.m_uID = dto.orderUid;
                    if (HasClearField(dto, "platingStepId")) node.m_platingStep = null;
                    ApplyNodeIcon(node, entry);
                    if (!string.IsNullOrEmpty(dto.platingStepId))
                    {
                        var plating = FindPlatingStep(dto.platingStepId);
                        if (plating == null)
                            return "未找到装盘容器：" + dto.platingStepId;
                        node.m_platingStep = plating;
                    }
                    EditorUtility.SetDirty(node);
                }
            }
            if (!string.IsNullOrEmpty(dto.iconBase64))
            {
                var iconErr = SaveIcon(setName, entry, dto);
                if (iconErr != null)
                    return iconErr;
            }
            if (dto.modelFiles != null && dto.modelFiles.Length > 0)
            {
                var modelErr = SaveModel(setName, entry, dto);
                if (modelErr != null)
                    return modelErr;
            }
            else
            {
                // 无新文件也允许仅调整模型变换（历史版本只在上传新文件时才写变换，导致白改）。
                var tErr = ApplyModelTransform(entry, dto);
                if (tErr != null)
                    return tErr;
            }
            PersistLookupAssets(entry, lookupPlan, setName);
            if (lookupPlan != null)
            {
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(dto.assetPath);
                var boundInstance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                if (boundInstance == null) throw new InvalidOperationException("实例化匹配表目标失败。");
                try
                {
                    ApplyLookupBindings(boundInstance, entry, lookupPlan);
                    PrefabUtility.ReplacePrefab(boundInstance, prefab, ReplacePrefabOptions.ReplaceNameBased);
                }
                finally { UnityEngine.Object.DestroyImmediate(boundInstance); }
            }
            EditorUtility.SetDirty(config);
        // SaveIcon/SaveModel 会回填 entry.iconPath 等字段（entry 是嵌套序列化对象，须显式标脏 config）。
        EditorUtility.SetDirty(config);
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

    private static bool HasClearField(CustomContentEditDto dto, string name)
    {
        if (dto == null || dto.clearFields == null)
            return false;
        for (int i = 0; i < dto.clearFields.Length; i++)
        {
            if (dto.clearFields[i] == name)
                return true;
        }
        return false;
    }

    /// <summary>按 clearFields 名单显式清空引用字段（空 guid = 保持原值的旧语义不变）。</summary>
    private static void ApplyClearFields(GameObject instance, CustomContentEditDto dto)
    {
        if (instance == null || dto == null || dto.clearFields == null || dto.clearFields.Length == 0)
            return;
        for (int i = 0; i < dto.clearFields.Length; i++)
        {
            var f = dto.clearFields[i];
            if (string.IsNullOrEmpty(f))
                continue;
            if (f == "progressUiGuid")
            {
                ClearSetupField(instance, "SetupCustomIngredientStub", "progressUIPrefabSO");
                ClearSetupField(instance, "SetupCustomCounterStub", "progressUIPrefabSO");
                ClearSetupField(instance, "SetupCustomUtensilStub", "progressUIPrefabSO");
            }
            else if (f == "ingredientContentUiGuid")
            {
                ClearSetupField(instance, "SetupCustomIngredientStub", "ingredientContentUIPrefabSO");
                ClearSetupField(instance, "SetupCustomUtensilStub", "ingredientContentUIPrefabSO");
            }
            else if (f == "fireEffectGuid") ClearSetupField(instance, "SetupCustomCounterStub", "fireEffectPrefabSO");
            else if (f == "chopPfxGuid") ClearSetupField(instance, "SetupCustomCounterStub", "chopPFXSO");
            else if (f == "tutorialIconGuid") ClearSetupField(instance, "SetupCustomCounterStub", "tutorialIconUISO");
            else if (f == "sludgeGuid") ClearSetupField(instance, "SetupCustomUtensilStub", "sludgeSO");
            else if (f == "spawnEffectGuid") ClearSetupField(instance, "SetupCustomUtensilStub", "spawnEffectSO");
            else if (f == "cleanPlateStackGuid") ClearSetupField(instance, "SetupCustomSinkStub", "cleanPlateStackSO");
            else if (f == "washingPartPath") ClearSetupField(instance, "SetupCustomSinkStub", "washingPart");
            else if (f == "dryingPartPath") ClearSetupField(instance, "SetupCustomSinkStub", "dryingPart");
            else if (f == "restrictionsGuid") ClearComponentField(instance, "PreparationContainer", "m_containerRestrictions");
            else if (f == "cosmeticsPrefabGuid") ClearComponentField(instance, "PreparationContainer", "m_cosmeticsPrefab");
            else if (f == "nextPrefabGuid") ClearComponentField(instance, "WorkableItem", "m_nextPrefab");
            else if (f == "traySlotGuid") ClearSetupArray(instance, "SetupCustomTrayStub", "slots");
            else if (f == "inputRecipeGuid") ClearComponentField(instance, "IngredientSplitter", "inputRecipe");
            else if (f == "outputPrefabGuid")
            {
                ClearComponentField(instance, "IngredientSplitter", "outputPrefabSO");
                ClearComponentField(instance, "IngredientSplitter", "outputPrefabLegacy");
            }
            // platingStepId 在 OrderNode 上清除（见 Update）。
        }
    }

    private static void ClearSetupField(GameObject instance, string typeName, string fieldName)
    {
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
        p.objectReferenceValue = null;
        so.ApplyModifiedProperties();
    }

    private static void ClearSetupArray(GameObject instance, string typeName, string fieldName)
    {
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
        p.arraySize = 0;
        so.ApplyModifiedProperties();
    }

    private static void ClearComponentField(GameObject instance, string componentName, string fieldName)
    {
        var comp = instance.GetComponent(componentName);
        if (comp == null)
            return;
        var so = new SerializedObject(comp);
        var p = so.FindProperty(fieldName);
        if (p == null)
            return;
        p.objectReferenceValue = null;
        so.ApplyModifiedProperties();
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
        // 先删资产目录，成功后再移除 Config 条目：避免目录删除失败而条目已被持久化移除，
        // 导致资产存在但列表不可见的孤儿状态。
        if (!AssetDatabase.DeleteAsset(itemDir))
            return "删除目录失败。";
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
                labelEn = StationLabelEn(names[i])
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
            {
                // wire 字段名保持 outputPrefabGuid，实际写入 PseudoPrefabSO（上游 2026-11 字段变更）。
                var outputSo = LoadAsset<PseudoPrefabSO>(dto.outputPrefabGuid);
                if (outputSo != null)
                {
                    SetObject(so, "outputPrefabSO", outputSo);
                    SetObject(so, "outputPrefabLegacy", null);
                }
                else
                    LayoutEditorLog.LogWarning("[custom-content] 分料机输出必须是 PseudoPrefabSO: " + dto.outputPrefabGuid);
            }
            if (!string.IsNullOrEmpty(dto.splitAudioTag))
                SetEnum(so, "splitAudioTag", "LevelEditorStub.LevelInfoSO+GameOneShotAudioTag", dto.splitAudioTag);
            // 上游分料机进度条依赖 cookingUISO；模板默认未赋值，新建/更新时补默认，
            // 避免编辑器 Play 在 StartSynchronising 里空引用。
            var cookingUI = so.FindProperty("cookingUISO");
            if (cookingUI != null && cookingUI.objectReferenceValue == null)
                cookingUI.objectReferenceValue = AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(CookingUI);
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
        var path = AssetDatabase.GUIDToAssetPath(guid);
        var list = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<OrderToPrefabListSO>(path);
        if (!IsLookupPersistent(list)) throw new InvalidOperationException("槽位 GUID 无法解析到持久化 OrderToPrefabListSO。");
        if (p.arraySize > 0 && p.GetArrayElementAtIndex(0).objectReferenceValue == list) return;
        if (p.arraySize == 0) p.arraySize = 1;
        for (int i = 0; i < p.arraySize; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = list;
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
        var missing = 0;
        for (int i = 0; i < guids.Length; i++)
        {
            var obj = gameObject ? (UnityEngine.Object)LoadAsset<GameObject>(guids[i]) : LoadAsset<PseudoPrefabSO>(guids[i]);
            if (obj == null && !string.IsNullOrEmpty(guids[i]))
                missing++;
            p.GetArrayElementAtIndex(i).objectReferenceValue = obj;
        }
        if (missing > 0)
            Debug.LogWarning("[CustomContent] " + field + " 有 " + missing + " 个引用 GUID 无法解析，已写为空。");
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
        var invalid = new List<string>();
        for (int i = 0; i < names.Length; i++)
        {
            var value = LevelEditorStub.PseudoPrefabCookingUtensilStub.CookingStationType.Invalid;
            try { value = (LevelEditorStub.PseudoPrefabCookingUtensilStub.CookingStationType)Enum.Parse(enumType, names[i], true); }
            catch { invalid.Add(names[i]); }
            p.GetArrayElementAtIndex(i).enumValueIndex = (int)value;
        }
        if (invalid.Count > 0)
            Debug.LogWarning("[CustomContent] 站台类型无法识别：" + string.Join(", ", invalid.ToArray()) + "，已按 Invalid 写入。");
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
        for (int i = 0; i < asms.Length; i++)
        {
            t = asms[i].GetType("LevelEditorStub." + name);
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
        // ① 真实 PlatingStepData 工程资产（common04/_supplies/PlatingStepData 的
        //    Plate/Glass/Mug 提取副本）：序列化为合法 GUID 引用，可安全写进磁盘资产。
        var realRoots = new[] { "Assets/common04/_supplies/PlatingStepData" };
        for (int i = 0; i < realRoots.Length; i++)
        {
            if (!AssetDatabase.IsValidFolder(realRoots[i]))
                continue;
            var realGuids = AssetDatabase.FindAssets("t:PlatingStepData", new[] { realRoots[i] });
            for (int j = 0; j < realGuids.Length; j++)
            {
                var rp = AssetDatabase.GUIDToAssetPath(realGuids[j]);
                if (Path.GetFileNameWithoutExtension(rp) != id)
                    continue;
                var data = AssetDatabase.LoadAssetAtPath<PlatingStepData>(rp);
                if (data != null)
                    return data;
            }
        }
        // ② PseudoPrefabSO 包装：仅接受 asset 直引（工程内资产）。
        //  ⚠ 绝不可把 AssetBundle.LoadAsset 的运行时实例写进磁盘资产——Unity 会序列化成
        //  「fileID≠0 + guid 全零」的损坏引用（Could not extract GUID 刷屏且引用不可用）。
        //  bundle 型包装（common01..03 的 bundle18 等）在此返回 null，由调用方明确报错。
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

    /// <summary>写入图标 PNG 并挂到 OrderNode；失败返回错误（HTTP 透传给前端）。</summary>
    private static string SaveIcon(string setName, CustomContentConfigSO.Entry entry, CustomContentEditDto dto)
    {
        if (entry == null || string.IsNullOrEmpty(dto.iconBase64))
            return null;
        var dir = Path.GetDirectoryName(entry.prefabPath).Replace("\\", "/");
        var id = Path.GetFileNameWithoutExtension(entry.prefabPath);
        var png = dir + "/" + id + "_Icon.png";
        try
        {
            var bytes = Convert.FromBase64String(dto.iconBase64);
            // PNG magic：防止 JPEG 等字节流伪装 .png 导致 Unity 导入失败、清空模板默认图标。
            if (bytes.Length < 8 || bytes[0] != 0x89 || bytes[1] != 0x50 || bytes[2] != 0x4E || bytes[3] != 0x47)
                return "图标必须是 PNG 格式（检测到非 PNG 数据）。请上传 PNG，或让浏览器端转换后重试。";
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
            InheritInfoBundle(png);
            var node = AssetDatabase.LoadAssetAtPath<IngredientOrderNode>(entry.nodePath);
            if (node != null)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(png);
                if (sprite == null)
                    return "图标导入失败（Sprite 为空）：" + png;
                node.m_iconSprite = sprite;
                node.m_crateLid = new SubTexture2D
                {
                    m_atlasTexture = sprite.texture,
                    m_subRect = sprite.textureRect
                };
                EditorUtility.SetDirty(node);
            }
            return null;
        }
        catch (Exception ex)
        {
            return "图标写入失败：" + ex.Message;
        }
    }

    /// <summary>写入模型文件（FBX/OBJ/贴图）+ 生成/替换 &lt;id&gt;_Model.prefab + 挂载变换。</summary>
    private static string SaveModel(string setName, CustomContentConfigSO.Entry entry, CustomContentEditDto dto)
    {
        if (entry == null || dto.modelFiles == null || dto.modelFiles.Length == 0)
            return null;
        var itemDir = Path.GetDirectoryName(entry.prefabPath).Replace("\\", "/");
        var id = Path.GetFileNameWithoutExtension(entry.prefabPath);
        var modelsDir = itemDir + "/models";
        if (!AssetDatabase.IsValidFolder(modelsDir))
            AssetDatabase.CreateFolder(itemDir, "models");
        try
        {
            string mainModel = null;
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < dto.modelFiles.Length; i++)
            {
                var f = dto.modelFiles[i];
                if (f == null || string.IsNullOrEmpty(f.fileName) || string.IsNullOrEmpty(f.base64))
                    continue;
                var name = UniqueFileName(SanitizeFileName(f.fileName), usedNames);
                if (string.IsNullOrEmpty(name)) continue;
                var path = modelsDir + "/" + name;
                File.WriteAllBytes(AbsPath(path), Convert.FromBase64String(f.base64));
                InheritInfoBundle(path);
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
                    InheritInfoBundle(modelsDir + "/" + n);
                }
            }
            if (string.IsNullOrEmpty(mainModel))
                return "模型文件中未找到 .fbx / .obj 主模型，请至少包含一个模型文件。";
            var imported = AssetDatabase.LoadAssetAtPath<GameObject>(mainModel);
            if (imported == null)
                return "模型导入失败（Unity 无法加载，请检查 FBX/OBJ 是否损坏）：" + mainModel;
            var modelPrefabPath = modelsDir + "/" + id + "_Model.prefab";
            EnsureModelPrefab(modelPrefabPath, imported, id);
            InheritInfoBundle(modelPrefabPath);
            return AttachModelToPrefab(entry, dto, modelPrefabPath);
        }
        catch (Exception ex)
        {
            return "模型写入失败：" + ex.Message;
        }
    }

    /// <summary>食材目录（ScanIngredients）补充：custom_content 条目的显示名与图标 URL。
    /// SO 与条目 prefab 同目录，按目录匹配 Config 条目；未命中返回 false（调用方走通用回退）。</summary>
    public static bool TryGetIngredientDisplay(string soAssetPath, out string nameZh, out string nameEn, out string iconUrl)
    {
        nameZh = null;
        nameEn = null;
        iconUrl = null;
        if (string.IsNullOrEmpty(soAssetPath) || soAssetPath.IndexOf("/" + ContentDir + "/", StringComparison.Ordinal) < 0)
            return false;
        var setName = SetNameFromPath(soAssetPath);
        if (string.IsNullOrEmpty(setName))
            return false;
        var config = LoadConfig(setName);
        if (config == null || config.entries == null)
            return false;
        var dir = Path.GetDirectoryName(soAssetPath);
        if (string.IsNullOrEmpty(dir))
            return false;
        for (int i = 0; i < config.entries.Length; i++)
        {
            var e = config.entries[i];
            if (e == null || string.IsNullOrEmpty(e.prefabPath))
                continue;
            if (!string.Equals(Path.GetDirectoryName(e.prefabPath), dir, StringComparison.Ordinal))
                continue;
            nameZh = string.IsNullOrEmpty(e.nameZh) ? e.id : e.nameZh;
            nameEn = string.IsNullOrEmpty(e.nameEn) ? e.id : e.nameEn;
            if (!string.IsNullOrEmpty(e.iconPath))
                iconUrl = "/api/custom-content/icon?assetPath=" + Uri.EscapeDataString(e.prefabPath)
                    + (string.IsNullOrEmpty(e.updatedAt) ? "" : "&v=" + Uri.EscapeDataString(e.updatedAt));
            return true;
        }
        return false;
    }

    /// <summary>仅更新模型变换（未上传新文件）。条目尚无模型时静默跳过。</summary>
    private static string ApplyModelTransform(CustomContentConfigSO.Entry entry, CustomContentEditDto dto)
    {
        if (entry == null)
            return null;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(entry.prefabPath);
        if (prefab == null || prefab.transform.Find("WebCustomModel") == null)
            return null;
        var itemDir = Path.GetDirectoryName(entry.prefabPath).Replace("\\", "/");
        var id = Path.GetFileNameWithoutExtension(entry.prefabPath);
        var modelPrefabPath = itemDir + "/models/" + id + "_Model.prefab";
        return AttachModelToPrefab(entry, dto, modelPrefabPath);
    }

    /// <summary>生成或原地替换 &lt;id&gt;_Model.prefab（保留 GUID，外部引用不失效）。</summary>
    private static void EnsureModelPrefab(string modelPrefabPath, GameObject imported, string id)
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(modelPrefabPath);
        if (existing == null)
        {
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
            return;
        }
        var inst = PrefabUtility.InstantiatePrefab(existing) as GameObject;
        if (inst == null)
            return;
        try
        {
            for (int i = inst.transform.childCount - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(inst.transform.GetChild(i).gameObject);
            var model = UnityEngine.Object.Instantiate(imported);
            model.name = imported.name;
            model.transform.SetParent(inst.transform, false);
            PrefabUtility.ReplacePrefab(inst, existing, ReplacePrefabOptions.ReplaceNameBased);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(inst);
        }
    }

    /// <summary>把模型 prefab 挂载为条目 prefab 的 WebCustomModel 子物体并应用变换。</summary>
    private static string AttachModelToPrefab(CustomContentConfigSO.Entry entry, CustomContentEditDto dto, string modelPrefabPath)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(entry.prefabPath);
        var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelPrefabPath);
        if (prefab == null || modelPrefab == null)
            return "模型挂载失败：条目 prefab 或模型 prefab 缺失。";
        var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance == null)
            return "实例化 prefab 失败。";
        try
        {
            var existingChild = instance.transform.Find("WebCustomModel");
            if (existingChild != null)
                UnityEngine.Object.DestroyImmediate(existingChild.gameObject);
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].enabled = false;
            var model = PrefabUtility.InstantiatePrefab(modelPrefab) as GameObject;
            if (model == null)
                return "实例化模型 prefab 失败：" + modelPrefabPath;
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
            return null;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    /// <summary>同批次文件名去重：清洗后重名时追加 _2/_3 序号，避免互相覆盖。</summary>
    private static string UniqueFileName(string name, HashSet<string> used)
    {
        if (string.IsNullOrEmpty(name))
            return "";
        if (used.Add(name))
            return name;
        var ext = Path.GetExtension(name);
        var stem = name.Substring(0, name.Length - ext.Length);
        for (int n = 2; n < 1000; n++)
        {
            var candidate = stem + "_" + n + ext;
            if (used.Add(candidate))
                return candidate;
        }
        return "";
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

    /// <summary>严格 id 校验（仅 ASCII 字母/数字/下划线），拒绝而非静默改写。</summary>
    private static bool IsValidId(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return false;
        for (int i = 0; i < raw.Length; i++)
        {
            var c = raw[i];
            if (!IsAsciiLetterOrDigit(c) && c != '_')
                return false;
        }
        return true;
    }

    private static bool IsAsciiLetterOrDigit(char c)
    {
        return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');
    }

    private static string SanitizeFileName(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        var name = Path.GetFileName(raw).Trim();
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (IsAsciiLetterOrDigit(c) || c == '_' || c == '-' || c == '.')
            {
                sb.Append(c);
            }
            else if (sb.Length == 0 || sb[sb.Length - 1] != '_')
            {
                // 非 ASCII（中文/空格等）折叠为单个下划线，避免不同文件名清洗后同名互相覆盖。
                sb.Append('_');
            }
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

    private static void InheritInfoBundle(string assetPath)
    {
        // Local content inherits the set's info bundle, including copied template labels.
        if (!assetPath.StartsWith(LevelSetsRoot + "/", StringComparison.Ordinal))
            throw new InvalidOperationException("Custom content bundle ownership must stay inside LevelSets: " + assetPath);
        var imp = AssetImporter.GetAtPath(assetPath);
        if (imp == null) return;
        if (!string.IsNullOrEmpty(imp.assetBundleName) || !string.IsNullOrEmpty(imp.assetBundleVariant))
        {
            imp.SetAssetBundleNameAndVariant("", "");
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

    private static string StationLabelEn(string name)
    {
        switch (name)
        {
            case "Hob": return "Hob stove";
            case "Oven": return "Oven";
            case "DeepFatFryer": return "Deep fat fryer";
            case "FirePit": return "Fire pit / griddle";
            case "Flamethrower": return "Flamethrower";
            case "Barbeque": return "Barbeque / skewer";
            case "Invalid_FloorBurner": return "Hotpot floor burner";
            default: return name;
        }
    }
}
