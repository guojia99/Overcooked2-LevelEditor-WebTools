using System;
using System.Reflection;
using UnityEngine;

namespace CustomStub
{
    internal static class WebBackpackProtocolLoader
    {
        private static float s_nextAttempt;
        internal static Type FindOrLoad()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType("BackpackProtocol.Bootstrap", false);
                if (type != null) return type;
            }
            if (Time.realtimeSinceStartup < s_nextAttempt) return null;
            s_nextAttempt = Time.realtimeSinceStartup + 5f;
            try
            {
                // Editor Play does not run Loader.dll. Resolve the staged TextAsset without
                // a compile-time UnityEditor dependency or reading arbitrary filesystem DLLs.
                var database = Type.GetType("UnityEditor.AssetDatabase, UnityEditor");
                var load = database != null ? database.GetMethod("LoadAssetAtPath",
                    BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string), typeof(Type) }, null) : null;
                var asset = load != null ? load.Invoke(null, new object[] {
                    "Assets/WebCustomStubRuntime/RuntimeDll/BackpackProtocol.dll.bytes", typeof(TextAsset) }) as TextAsset : null;
                if (asset != null) return Load(asset);
                foreach (var bundle in AssetBundle.GetAllLoadedAssetBundles())
                {
                    if (bundle == null || bundle.isStreamedSceneAssetBundle) continue;
                    foreach (var path in bundle.GetAllAssetNames())
                        if (path.EndsWith("/BackpackProtocol.dll.bytes", StringComparison.OrdinalIgnoreCase))
                        {
                            asset = bundle.LoadAsset<TextAsset>(path);
                            if (asset != null) return Load(asset);
                        }
                }
            }
            catch (Exception ex)
            {
                WebBackpackPrefabDirectory.Diagnostic("protocol-load", "staged protocol load failed: " + ex.Message);
            }
            return null;
        }

        private static Type Load(TextAsset asset)
        {
            var assembly = Assembly.Load(asset.bytes);
            if (assembly.GetName().Name != "BackpackProtocol")
                throw new InvalidOperationException("staged protocol assembly identity mismatch");
            return assembly.GetType("BackpackProtocol.Bootstrap", true);
        }
    }
}
