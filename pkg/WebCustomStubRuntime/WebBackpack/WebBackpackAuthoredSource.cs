using System;
using System.Reflection;
using LevelEditorStub;
using UnityEngine;

namespace CustomStub
{
    internal static class WebBackpackAuthoredSource
    {
        internal static GameObject Load(PseudoPrefabSO so)
        {
            if (so == null)
                return null;
            var direct = GameApi.GetPseudoPrefabDirectAsset(so) as GameObject;
            if (direct != null)
                return direct;
            var bundle = GameApi.GetAssetBundle(so.bundleName);
            return bundle != null ? bundle.LoadAsset(so.assetPath) as GameObject : null;
        }

        internal static GameObject ExactChild(GameObject wrapper)
        {
            // Shared authored wrapper contract: the real instantiated prefab is the unique
            // direct child with native carry/Workable/order components. Never match names/UIDs.
            GameObject found = null;
            for (int i = 0; i < wrapper.transform.childCount; i++)
            {
                var child = wrapper.transform.GetChild(i).gameObject;
                bool physical = Has(child, "WorkableItem") || Has(child, "IngredientPropertiesComponent")
                    || Has(child, "PhysicalAttachment") || Has(child, "Attachment");
                if (!physical)
                    continue;
                if (found != null)
                    return null;
                found = child;
            }
            return found;
        }

        private static bool Has(GameObject item, string typeName)
        {
            var type = GameApi.Find(typeName);
            return type != null && item.GetComponent(type) != null;
        }

        internal static PseudoPrefabSO Source(Component component)
        {
            if (component == null || component.GetType().Assembly != typeof(PseudoPrefabSO).Assembly)
                return null;
            // Only the shared LevelEditorStub source field, not arbitrary SO decoration fields.
            var field = component.GetType().GetField("pseudoPrefabSO", BindingFlags.Public | BindingFlags.Instance);
            return field != null && typeof(PseudoPrefabSO).IsAssignableFrom(field.FieldType)
                ? field.GetValue(component) as PseudoPrefabSO : null;
        }
    }
}
