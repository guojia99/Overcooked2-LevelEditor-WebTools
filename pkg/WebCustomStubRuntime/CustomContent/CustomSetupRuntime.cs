using System;
using System.Collections.Generic;
using System.Reflection;
using LevelEditorStub;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CustomStub
{
    /// <summary>
    /// 上游 SetupCustomIngredient/Counter/Utensil/Sink/Tray 的真机等价实现。
    /// 这些 SetupCustom* 类在 LevelEditor（编辑器宿主）程序集，游戏侧不存在；
    /// 本类只读 SetupCustom*Stub 的序列化字段，把目标字段写进游戏原生组件。
    /// 反射读取类型/字段，兼容旧版 LevelEditorStub.dll（缺字段时静默跳过）。
    /// </summary>
    internal static class CustomSetupRuntime
    {
        private static readonly HashSet<string> s_warned = new HashSet<string>(StringComparer.Ordinal);

        internal static void Apply(Scene scene)
        {
            if (!scene.isLoaded)
                return;
            try
            {
                var all = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
                for (int i = 0; i < all.Length; i++)
                {
                    var mb = all[i];
                    if (mb == null)
                        continue;
                    var n = mb.GetType().Name;
                    if (n == "SetupCustomIngredientStub")
                        ApplyIngredient(mb);
                    else if (n == "SetupCustomCounterStub")
                        ApplyCounter(mb);
                    else if (n == "SetupCustomUtensilStub")
                        ApplyUtensil(mb);
                    else if (n == "SetupCustomSinkStub")
                    {
                        ApplyCounter(mb);
                        ApplySink(mb);
                    }
                    else if (n == "SetupCustomTrayStub")
                    {
                        ApplyUtensil(mb);
                        ApplyTray(mb);
                    }
                }
            }
            catch (Exception ex)
            {
                WarnOnce("apply", "[CustomStub] SetupCustom 运行时应用失败: " + ex.Message);
            }
        }

        private static void ApplyIngredient(MonoBehaviour stub)
        {
            var go = stub.gameObject;
            var contentUi = LoadObject(ReadSo(stub, "ingredientContentUIPrefabSO"));
            var throwParticle = LoadObject(ReadSo(stub, "throwParticleSO"));
            var progressUi = LoadObject(ReadSo(stub, "progressUIPrefabSO"));

            var content = go.GetComponent("IngredientContentGUI");
            if (content != null && contentUi != null)
                SetField(content, "m_ingredientContentUIPrefab", contentUi.GetComponent("IngredientContentsUIContainer"));
            var throwable = go.GetComponent("ThrowableItem");
            if (throwable != null && throwParticle != null)
                SetField(throwable, "m_throwParticle", throwParticle);
            var workable = go.GetComponent("WorkableItem");
            if (workable != null && progressUi != null)
                SetField(workable, "m_progressUIPrefab", progressUi.GetComponent("ProgressUIController"));
        }

        private static void ApplyCounter(MonoBehaviour stub)
        {
            var go = stub.gameObject;
            var progressUi = LoadObject(ReadSo(stub, "progressUIPrefabSO"));
            var fireFx = LoadObject(ReadSo(stub, "fireEffectPrefabSO"));
            var chopPfx = LoadObject(ReadSo(stub, "chopPFXSO"));
            var tutorialIcon = LoadObject(ReadSo(stub, "tutorialIconUISO"));

            var flammable = go.GetComponent("Flammable");
            if (flammable != null)
            {
                if (progressUi != null)
                    SetField(flammable, "m_progressUIPrefab", progressUi.GetComponent("ProgressUIController"));
                if (fireFx != null)
                    SetField(flammable, "m_fireEffectPrefab", fireFx);
            }
            var workstation = go.GetComponent("Workstation");
            if (workstation != null && chopPfx != null)
                SetField(workstation, "m_chopPFX", chopPfx);
            var hover = go.GetComponent("ButtonHoverIcon");
            if (hover != null && tutorialIcon != null)
                SetField(hover, "m_iconPrefab", tutorialIcon);
        }

        private static void ApplyUtensil(MonoBehaviour stub)
        {
            var go = stub.gameObject;
            var contentUi = LoadObject(ReadSo(stub, "ingredientContentUIPrefabSO"));
            var sludge = LoadObject(ReadSo(stub, "sludgeSO"));
            var spawnFx = LoadObject(ReadSo(stub, "spawnEffectSO"));
            var progressUi = LoadObject(ReadSo(stub, "progressUIPrefabSO"));

            var content = go.GetComponent("IngredientContentGUI");
            if (content != null && contentUi != null)
                SetField(content, "m_ingredientContentUIPrefab", contentUi.GetComponent("IngredientContentsUIContainer"));
            var attached = go.GetComponent("AttachedOrderCosmeticDecisions");
            if (attached != null && sludge != null)
                SetField(attached, "m_noMatchingRecipePrefab", sludge);
            var respawn = go.GetComponent("UtensilRespawnBehaviour");
            if (respawn != null && spawnFx != null)
                SetField(respawn, "m_spawnEffect", spawnFx);
            var washable = go.GetComponent("Washable");
            if (washable != null && progressUi != null)
                SetField(washable, "m_progressUIPrefab", progressUi.GetComponent("ProgressUIController"));
        }

        private static void ApplySink(MonoBehaviour stub)
        {
            var go = stub.gameObject;
            var progressUi = LoadObject(ReadSo(stub, "progressUIPrefabSO"));
            var cleanStack = LoadObject(ReadSo(stub, "cleanPlateStackSO"));
            var washingPart = ReadObject(stub, "washingPart") as GameObject;
            var dryingPart = ReadObject(stub, "dryingPart") as GameObject;

            if (washingPart != null)
            {
                var washing = washingPart.GetComponent("WashingStation");
                if (washing != null && progressUi != null)
                    SetField(washing, "m_progressUIPrefab", progressUi.GetComponent("ProgressUIController"));
            }
            if (dryingPart != null && cleanStack != null)
            {
                var drying = dryingPart.GetComponent("PlateReturnStation");
                if (drying != null)
                    SetField(drying, "m_stackPrefab", cleanStack);
            }
        }

        private static void ApplyTray(MonoBehaviour stub)
        {
            var go = stub.gameObject;
            var sludge = LoadObject(ReadSo(stub, "sludgeSO"));
            var tray = go.GetComponent("TrayCosmeticDecisions");
            if (tray != null && sludge != null)
                SetField(tray, "m_noMatchingRecipePrefab", sludge);
            // Tray.m_slots 的 OrderToPrefabLookup 构建需要 RecipeHelper；优先使用
            // 编辑器烘焙的 slots（真机 prefab 已序列化）。stub.slots 仅在为空时尝试。
            var trayComp = go.GetComponent("Tray");
            if (trayComp == null)
                return;
            var slots = ReadObject(stub, "slots") as Array;
            if (slots == null || slots.Length == 0)
                return;
            var current = GetField(trayComp, "m_slots") as Array;
            if (current != null && current.Length > 0)
                return;
            var built = BuildLookups(slots);
            if (built != null && built.Length > 0)
            {
                var f = trayComp.GetType().GetField("m_slots",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (f != null)
                    f.SetValue(trayComp, built);
            }
            else
            {
                WarnOnce("traySlots", "[CustomStub] 自定义托盘 slots 构建失败（需要 recipes/modelSOs）；真机使用 prefab 默认槽位");
            }
        }

        private static Array BuildLookups(Array listSos)
        {
            if (listSos == null || GameApi.OrderToPrefabLookupType == null
                || GameApi.ContentPrefabLookupType == null || GameApi.LookupArrayField == null
                || GameApi.LookupContentField == null || GameApi.LookupPrefabField == null)
                return null;
            var lookups = Array.CreateInstance(GameApi.OrderToPrefabLookupType, listSos.Length);
            var made = 0;
            for (int i = 0; i < listSos.Length; i++)
            {
                var list = listSos.GetValue(i);
                if (list == null)
                    continue;
                var lt = list.GetType();
                var recipesField = lt.GetField("recipes", BindingFlags.Public | BindingFlags.Instance);
                var modelsField = lt.GetField("models", BindingFlags.Public | BindingFlags.Instance);
                var modelSosField = lt.GetField("modelSOs", BindingFlags.Public | BindingFlags.Instance);
                var recipes = recipesField != null ? recipesField.GetValue(list) as Array : null;
                var models = modelsField != null ? modelsField.GetValue(list) as Array : null;
                var modelSos = modelSosField != null ? modelSosField.GetValue(list) as Array : null;
                if (recipes == null || recipes.Length == 0)
                    continue;
                var lookup = ScriptableObject.CreateInstance(GameApi.OrderToPrefabLookupType);
                var arr = Array.CreateInstance(GameApi.ContentPrefabLookupType, recipes.Length);
                var count = 0;
                for (int j = 0; j < recipes.Length; j++)
                {
                    var node = ResolveOrderNode(recipes.GetValue(j) as ScriptableObject);
                    if (node == null)
                        continue;
                    var item = Activator.CreateInstance(GameApi.ContentPrefabLookupType);
                    GameApi.LookupContentField.SetValue(item, node);
                    var model = j < (models != null ? models.Length : 0) ? models.GetValue(j) as GameObject : null;
                    if (model == null && j < (modelSos != null ? modelSos.Length : 0))
                    {
                        var mso = modelSos.GetValue(j) as PseudoPrefabSO;
                        if (mso != null) model = GameApi.LoadPseudoPrefabObject(mso);
                    }
                    GameApi.LookupPrefabField.SetValue(item, model);
                    var amountField = GameApi.ContentPrefabLookupType.GetField("m_amountAllowed");
                    if (amountField != null) amountField.SetValue(item, 1);
                    var restrictedField = GameApi.ContentPrefabLookupType.GetField("m_restrictedContent");
                    if (restrictedField != null && GameApi.OrderDefinitionNodeType != null)
                        restrictedField.SetValue(item, Array.CreateInstance(GameApi.OrderDefinitionNodeType, 0));
                    arr.SetValue(item, count++);
                }
                if (count == 0)
                    continue;
                if (count != arr.Length)
                {
                    var trimmed = Array.CreateInstance(GameApi.ContentPrefabLookupType, count);
                    Array.Copy(arr, trimmed, count);
                    arr = trimmed;
                }
                GameApi.LookupArrayField.SetValue(lookup, arr);
                if (GameApi.LookupCacheMethod != null)
                    GameApi.LookupCacheMethod.Invoke(lookup, null);
                lookups.SetValue(lookup, made++);
            }
            if (made == 0)
                return null;
            if (made != lookups.Length)
            {
                var trimmed = Array.CreateInstance(GameApi.OrderToPrefabLookupType, made);
                Array.Copy(lookups, trimmed, made);
                lookups = trimmed;
            }
            return lookups;
        }

        private static object ResolveOrderNode(ScriptableObject recipe)
        {
            if (recipe == null)
                return null;
            var pseudo = recipe as PseudoPrefabSO;
            if (pseudo != null)
            {
                var prefab = GameApi.LoadPseudoPrefabObject(pseudo);
                if (prefab != null)
                {
                    var props = prefab.GetComponent("IngredientPropertiesComponent");
                    if (props != null)
                    {
                        var f = props.GetType().GetField("m_ingredientOrderNode",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        var node = f != null ? f.GetValue(props) : null;
                        if (node != null) return node;
                    }
                }
                var direct = GameApi.LoadPseudoPrefabAsset(pseudo, GameApi.OrderDefinitionNodeType);
                if (direct != null) return direct;
            }
            var custom = recipe as CustomRecipeSO;
            if (custom != null && custom.compositionSOs != null && custom.compositionSOs.Length > 0)
                return ResolveOrderNode(custom.compositionSOs[0]);
            return null;
        }

        private static PseudoPrefabSO ReadSo(MonoBehaviour stub, string field)
        {
            return ReadObject(stub, field) as PseudoPrefabSO;
        }

        private static object ReadObject(MonoBehaviour stub, string field)
        {
            try
            {
                var f = stub.GetType().GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                return f != null ? f.GetValue(stub) : null;
            }
            catch
            {
                return null;
            }
        }

        private static GameObject LoadObject(PseudoPrefabSO so)
        {
            if (so == null)
                return null;
            return GameApi.LoadPseudoPrefabObject(so);
        }

        private static void SetField(object target, string field, object value)
        {
            if (target == null || value == null)
                return;
            try
            {
                var f = target.GetType().GetField(field,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (f != null)
                    f.SetValue(target, value);
            }
            catch (Exception ex)
            {
                WarnOnce("field:" + field, "[CustomStub] SetupCustom 写字段失败 " + field + ": " + ex.Message);
            }
        }

        private static object GetField(object target, string field)
        {
            try
            {
                var f = target.GetType().GetField(field,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                return f != null ? f.GetValue(target) : null;
            }
            catch
            {
                return null;
            }
        }

        private static void WarnOnce(string key, string msg)
        {
            if (s_warned.Add(key))
                StubLog.LogWarn(msg);
        }
    }
}
