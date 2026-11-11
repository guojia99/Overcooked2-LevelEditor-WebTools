using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace CustomStub
{
    // Only plain salad leaves are eligible. Cooked/mixed/hotdog trees stay native.
    public static class SaladSauceRuntime
    {
        static bool s_installed;
        static bool s_active;
        static bool s_warned;
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        internal static void SetSceneActive(string[] features, LevelEditorStub.LevelInfoSO info)
        {
            bool active = false;
            if (features != null)
            {
                foreach (string feature in features)
                    active |= string.Equals(feature, "saladsauce", StringComparison.OrdinalIgnoreCase);
            }
            else if (info != null)
            {
                foreach (ScriptableObject recipe in info.recipes ?? new ScriptableObject[0])
                {
                    LevelEditorStub.CustomRecipeSO custom = recipe as LevelEditorStub.CustomRecipeSO;
                    if (custom != null && custom.score > 0 && custom.recipeName != null
                        && custom.recipeName.StartsWith("Web_SauceSalad_", StringComparison.Ordinal)) active = true;
                }
            }
            SetActive(active);
        }

        internal static void SetActive(bool active)
        {
            s_active = false;
            if (!active) return;
            if (s_installed) { s_active = true; return; }
            Harmony h = new Harmony("oc2.salad.sauces");
            try
            {
                Patch(h, "Plate", "CanPlaceOnPlate", "BeforePlate");
                Patch(h, "ServerPlacementContainer", "CanHandlePlacement", "BeforeCanPlace");
                Patch(h, "ServerPlacementContainer", "HandlePlacement", "BeforePlace");
                Patch(h, "AssembledNodeTransfer", "CanCombineWithContents", "BeforeCombine");
                Patch(h, "AssembledNodeTransfer", "CombineWithContents", "BeforeCombine");
                Patch(h, "ClientCondimentDispenserCosmeticDecisions", "OnItemSwitched", "BeforeDispenserSwitch");
                Patch(h, "ClientCondimentDispenserCosmeticDecisions", "OnPickupItem", "BeforeDispenserPickup");
                s_installed = true;
                s_active = true;
            }
            catch (Exception ex)
            {
                h.UnpatchSelf();
                Warn(ex);
            }
        }

        static void Warn(Exception ex)
        {
            if (s_warned) return;
            s_warned = true;
            StubLog.LogWarn("[SaladSauce] Runtime guard unavailable, retaining native behavior: " + ex);
        }

        static void Patch(Harmony h, string type, string method, string prefix)
        {
            Type t = GameApi.Find(type);
            MethodInfo target = t == null ? null : t.GetMethod(method, Flags | BindingFlags.Static);
            if (target == null) throw new MissingMethodException(type, method);
            h.Patch(target, new HarmonyMethod(typeof(SaladSauceRuntime).GetMethod(prefix, BindingFlags.NonPublic | BindingFlags.Static)));
        }

        public static FieldInfo Field(Type t, string name)
        {
            while (t != null)
            {
                FieldInfo f = t.GetField(name, Flags | BindingFlags.DeclaredOnly);
                if (f != null) return f;
                t = t.BaseType;
            }
            return null;
        }

        public static int CanonicalUid(int uid)
        {
            if (uid == 158482) return 890084;
            if (uid == 17094) return 890076;
            return uid;
        }

        // Native machine slots are [Mustard, Ketchup], independently of the
        // configured cycling list. Never change the machine's order definition.
        public static int DispenserVisualSlot(int uid)
        {
            int canonical = CanonicalUid(uid);
            return canonical == 890076 ? 0 : canonical == 890084 ? 1 : -1;
        }

        static bool BeforeDispenserSwitch(object __instance)
        {
            if (!s_active) return true;
            try
            {
                object definition = Field(__instance.GetType(), "m_orderDefinition").GetValue(__instance);
                object node = GameApi.Find("IOrderDefinition").GetMethod("GetOrderComposition").Invoke(definition, null);
                FieldInfo ingredient = node == null ? null : Field(node.GetType(), "m_ingriedientOrderNode");
                object source = ingredient == null ? null : ingredient.GetValue(node);
                if (source == null) return true;
                int slot = DispenserVisualSlot((int)Field(source.GetType(), "m_uID").GetValue(source));
                if (slot < 0) return true;
                Animator animator = Field(__instance.GetType(), "m_animator").GetValue(__instance) as Animator;
                if (animator == null) return true;
                animator.ResetTrigger(slot == 0 ? "Condiment2" : "Condiment1");
                animator.SetTrigger(slot == 0 ? "Condiment1" : "Condiment2");
                return false;
            }
            catch (Exception ex) { Warn(ex); return true; }
        }

        static bool BeforeDispenserPickup(object __instance)
        {
            if (!s_active) return true;
            try
            {
                object definition = Field(__instance.GetType(), "m_orderDefinition").GetValue(__instance);
                object node = GameApi.Find("IOrderDefinition").GetMethod("GetOrderComposition").Invoke(definition, null);
                FieldInfo ingredient = node == null ? null : Field(node.GetType(), "m_ingriedientOrderNode");
                object source = ingredient == null ? null : ingredient.GetValue(node);
                if (source == null) return true;
                int slot = DispenserVisualSlot((int)Field(source.GetType(), "m_uID").GetValue(source));
                if (slot < 0) return true;
                object cosmetic = Field(__instance.GetType(), "m_condimentDispenserCosmeticDecisions").GetValue(__instance);
                GameObject[] effects = Field(cosmetic.GetType(), "m_particleEffects").GetValue(cosmetic) as GameObject[];
                if (effects == null || slot >= effects.Length) return true;
                object audio = Field(cosmetic.GetType(), "m_audioTag").GetValue(cosmetic);
                MethodInfo trigger = GameApi.Find("GameUtils").GetMethod("TriggerAudio", BindingFlags.Static | BindingFlags.Public, null, new Type[] { audio.GetType(), typeof(int) }, null);
                if (trigger == null) return true;
                for (int i = 0; i < effects.Length; i++) effects[i].SetActive(i == slot);
                trigger.Invoke(null, new object[] { audio, ((Component)__instance).gameObject.layer });
                return false;
            }
            catch (Exception ex) { Warn(ex); return true; }
        }

        public static bool IsSaladUid(int uid)
        {
            return uid == 19478 || uid == 15580 || uid == 37422
                || uid == 152142 || uid == 151504 || uid == 151714
                || uid == 37874 || uid == 390802;
        }

        // Returns false on non-salad structures, deliberately without simplifying cooked nodes.
        static bool Leaves(object node, List<int> ids)
        {
            if (node == null) return false;
            Array arr = node as Array;
            if (arr != null)
            {
                foreach (object child in arr) if (!Leaves(child, ids)) return false;
                return true;
            }
            string name = node.GetType().Name;
            if (name == "CompositeAssembledNode")
            {
                FieldInfo composition = Field(node.GetType(), "m_composition");
                return composition != null && Leaves(composition.GetValue(node), ids);
            }
            if (name != "IngredientAssembledNode") return false;
            FieldInfo ingredient = Field(node.GetType(), "m_ingriedientOrderNode");
            object def = ingredient == null ? null : ingredient.GetValue(node);
            if (def == null || (def is UnityEngine.Object && (UnityEngine.Object)def == null)) return false;
            FieldInfo uidField = Field(def.GetType(), "m_uID");
            if (uidField == null) return false;
            int uid = CanonicalUid((int)uidField.GetValue(def));
            if (!IsSaladUid(uid) && uid != 890084 && uid != 890076) return false;
            ids.Add(uid);
            return true;
        }

        public static bool RejectContents(object current, object incoming)
        {
            List<int> a = new List<int>();
            List<int> b = new List<int>();
            if ((current != null && !Leaves(current, a)) || !Leaves(incoming, b)) return false;
            bool hasSauce = false;
            bool hasSalad = false;
            int red = 0, yellow = 0;
            foreach (int uid in a)
            {
                hasSalad |= IsSaladUid(uid);
                if (uid == 890084) red++;
                if (uid == 890076) yellow++;
            }
            foreach (int uid in b)
            {
                hasSalad |= IsSaladUid(uid);
                if (uid == 890084) { red++; hasSauce = true; }
                if (uid == 890076) { yellow++; hasSauce = true; }
            }
            return hasSauce && (!hasSalad || red > 1 || yellow > 1);
        }

        static object Contents(object adapter)
        {
            if (adapter == null) return null;
            Type contents = GameApi.Find("IIngredientContents");
            MethodInfo get = contents != null && contents.IsInstanceOfType(adapter)
                ? contents.GetMethod("GetContents") : adapter.GetType().GetMethod("GetContents", Flags);
            return get == null ? null : get.Invoke(adapter, null);
        }

        static object Composition(GameObject go)
        {
            if (go == null) return null;
            Type t = GameApi.Find("IOrderDefinition");
            Component c = t == null ? null : go.GetComponent(t);
            if (c == null)
            {
                t = GameApi.Find("IClientOrderDefinition");
                c = t == null ? null : go.GetComponent(t);
            }
            return c == null ? null : t.GetMethod("GetOrderComposition").Invoke(c, null);
        }

        static bool BeforePlate(GameObject _gameObject, object _ingredientContentsAdapter, ref bool __result)
        {
            if (!s_active) return true;
            try
            {
                if (_ingredientContentsAdapter == null || !RejectContents(Contents(_ingredientContentsAdapter), Composition(_gameObject))) return true;
                __result = false;
                return false;
            }
            catch (Exception ex) { Warn(ex); return true; }
        }

        static bool RejectPlacement(object instance, object carrier)
        {
            Component c = instance as Component;
            if (c == null || carrier == null) return false;
            if (GameApi.ICarrierInspectCarriedMethod == null) return false;
            GameObject carried = GameApi.ICarrierInspectCarriedMethod.Invoke(carrier, null) as GameObject;
            if (carried == null) return false;
            Type plate = GameApi.Find("Plate");
            Type contents = GameApi.Find("ServerIngredientContainer");
            if (plate == null || contents == null) return false;
            if (c.gameObject.GetComponent(plate) != null
                && RejectContents(Contents(c.gameObject.GetComponent(contents)), Composition(carried))) return true;
            return carried.GetComponent(plate) != null
                && RejectContents(Contents(carried.GetComponent(contents)), Composition(c.gameObject));
        }

        static bool BeforeCanPlace(object __instance, object _carrier, ref bool __result)
        {
            if (!s_active) return true;
            try
            {
                if (!RejectPlacement(__instance, _carrier)) return true;
                __result = false;
                return false;
            }
            catch (Exception ex) { Warn(ex); return true; }
        }

        static bool BeforePlace(object __instance, object _carrier)
        {
            if (!s_active) return true;
            try { return !RejectPlacement(__instance, _carrier); }
            catch (Exception ex) { Warn(ex); return true; }
        }

        // Normalize a COPY at the shared transfer boundary, only when salad is present.
        // Both the client's temporary adapter and the server's actual transfer use this.
        static void BeforeCombine(ref object _ingredientToAdd, object _container)
        {
            if (!s_active) return;
            try
            {
                if (_container == null) return;
                List<int> ids = new List<int>();
                if (!Leaves(Contents(_container), ids) || !Leaves(_ingredientToAdd, ids)) return;
                bool salad = false;
                foreach (int uid in ids) salad |= IsSaladUid(uid);
                if (salad) _ingredientToAdd = Normalize(_ingredientToAdd);
            }
            catch (Exception ex) { Warn(ex); }
        }

        static object Normalize(object node)
        {
            if (node == null) return null;
            Type t = node.GetType();
            if (t.Name == "IngredientAssembledNode")
            {
                object def = Field(t, "m_ingriedientOrderNode").GetValue(node);
                int uid = (int)Field(def.GetType(), "m_uID").GetValue(def);
                int canonical = CanonicalUid(uid);
                if (canonical == uid) return node;
                AssetBundle bundle = GameApi.GetAssetBundle("bundle427");
                object replacement = bundle == null ? null : bundle.LoadAsset(
                    "Assets/downloadablecontent/dlc11/dlc_assets/data/orderdefinitions/ingredients/"
                    + (canonical == 890084 ? "dlc11_ketchup.asset" : "dlc11_mustard.asset"), GameApi.Find("IngredientOrderNode"));
                if (replacement == null) throw new InvalidOperationException("Salad canonical sauce UID missing: " + canonical);
                return Activator.CreateInstance(t, new object[] { replacement });
            }
            if (t.Name != "CompositeAssembledNode") return node;
            FieldInfo composition = Field(t, "m_composition");
            Array original = composition.GetValue(node) as Array;
            Array normalized = (Array)original.Clone();
            bool changed = false;
            for (int i = 0; i < original.Length; i++)
            {
                object next = Normalize(original.GetValue(i));
                normalized.SetValue(next, i);
                changed |= !ReferenceEquals(next, original.GetValue(i));
            }
            if (!changed) return node;
            // Simpilfy makes a detached tree, retaining native permitted-entry metadata.
            object clone = t.GetMethod("Simpilfy").Invoke(node, null);
            FieldInfo f = Field(clone.GetType(), "m_composition");
            if (f == null) return Normalize(clone);
            f.SetValue(clone, normalized);
            return clone;
        }
    }
}
