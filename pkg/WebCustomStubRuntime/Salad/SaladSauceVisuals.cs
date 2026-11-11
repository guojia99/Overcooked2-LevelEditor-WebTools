using System;
using System.Reflection;
using LevelEditorStub;
using UnityEngine;

namespace CustomStub
{
    [DefaultExecutionOrder(-100)]
    public class SaladSauceVisuals : MonoBehaviour
    {
        public PseudoPrefabSO originalCarrier;
        public PseudoPrefabSO ketchup;
        public PseudoPrefabSO mustard;
        public PseudoPrefabSO ketchupAlias;
        public PseudoPrefabSO mustardAlias;
        public GameObject ketchupVisual;
        public GameObject mustardVisual;
        UnityEngine.Object m_lookup;

        void Awake()
        {
            if (!Application.isPlaying) return;
            try { BuildLookup(); }
            catch (Exception ex) { StubLog.LogWarn("[SaladSauce] Cannot build carrier lookup: " + ex); }
        }

        public void BuildLookup()
        {
            if (m_lookup != null) return;
            GameObject source = GameApi.LoadPseudoPrefabObject(originalCarrier);
            if (source == null) throw new InvalidOperationException("Original salad carrier unavailable");
            Type cosmeticType = GameApi.Find("SaladCosmeticDecisions");
            Component target = GameApi.GetComponent(gameObject, cosmeticType);
            Component original = GameApi.GetComponent(source, cosmeticType);
            if (target == null || original == null) throw new InvalidOperationException("Salad cosmetic component unavailable");
            FieldInfo lookupField = SaladSauceRuntime.Field(original.GetType(), "m_prefabLookup");
            if (lookupField == null) throw new MissingFieldException("Salad cosmetic lookup");
            UnityEngine.Object lookup = lookupField.GetValue(original) as UnityEngine.Object;
            if (lookup == null) throw new InvalidOperationException("Original salad lookup unavailable");
            FieldInfo arrField = SaladSauceRuntime.Field(lookup.GetType(), "m_lookupArray");
            if (arrField == null) throw new MissingFieldException("Salad lookup entries");
            Array arr = arrField.GetValue(lookup) as Array;
            if (arr == null) throw new InvalidOperationException("Original salad lookup entries unavailable");
            Type entryType = arr.GetType().GetElementType();
            Array extended = Array.CreateInstance(entryType, arr.Length + 4);
            Array.Copy(arr, extended, arr.Length);
            PseudoPrefabSO[] sos = new PseudoPrefabSO[] { ketchup, mustard, ketchupAlias, mustardAlias };
            for (int i = 0; i < sos.Length; i++)
            {
                object entry = Activator.CreateInstance(entryType);
                object node = GameApi.LoadPseudoPrefabAsset(sos[i], GameApi.Find("IngredientOrderNode"));
                if (node == null) throw new InvalidOperationException("Sauce node unavailable at slot " + i);
                int uid = (int)SaladSauceRuntime.Field(node.GetType(), "m_uID").GetValue(node);
                if (SaladSauceRuntime.DispenserVisualSlot(uid) != (i % 2 == 0 ? 1 : 0))
                    throw new InvalidOperationException("Sauce identity does not match visual slot: " + uid);
                if ((i % 2 == 0 ? ketchupVisual : mustardVisual) == null) throw new InvalidOperationException("Sauce visual unavailable at slot " + i);
                SaladSauceRuntime.Field(entryType, "m_content").SetValue(entry, node);
                SaladSauceRuntime.Field(entryType, "m_prefab").SetValue(entry, i % 2 == 0 ? ketchupVisual : mustardVisual);
                extended.SetValue(entry, arr.Length + i);
            }
            UnityEngine.Object built = UnityEngine.Object.Instantiate(lookup);
            try
            {
                arrField.SetValue(built, extended);
                SaladSauceRuntime.Field(built.GetType(), "m_cachedAssembledOrderNodes").SetValue(built, false);
                lookupField.SetValue(target, built);
                m_lookup = built;
            }
            finally
            {
                if (m_lookup == null && built != null)
                {
                    if (Application.isPlaying) UnityEngine.Object.Destroy(built);
                    else UnityEngine.Object.DestroyImmediate(built);
                }
            }
        }

        void OnDestroy()
        {
            if (m_lookup == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(m_lookup);
            else UnityEngine.Object.DestroyImmediate(m_lookup);
        }
    }
}
