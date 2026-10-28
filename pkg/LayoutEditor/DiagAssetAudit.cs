using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 资产审计（一次性工具，C#4 兼容）：
///  A. 逐个加载 bundle47/bundle38 的全部资产（每条前打 LOADING 标记，三条
///     "Unexpected node type" 会夹在具体资产名之间，定位失败资产）；
///  B. 输出三个 OBJ 的真实网格 sub-asset fileID（修复 MeshFilter 空引用的依据）；
///  C. 场景实例渲染器审计（kettle / Switch 的材质、shader、网格状态）。
/// 结果写 logs/asset_audit.txt。
/// </summary>
public static class DiagAssetAudit
{
    private const string ResultPath = "logs/asset_audit.txt";

    private static readonly string[] ObjPaths = new string[]
    {
        "Assets/commonW5/kettle/meshes/m_kettle_anniversary_01.obj",
        "Assets/commonW5/kettle/meshes/m_sk_cooker_anniversary_01.obj",
        "Assets/commonW5/kettle/meshes/m_dlc100_teacup_cup.obj",
    };

    private static readonly string[] BundleNames = new string[] { "bundle47", "bundle38" };

    private static StringBuilder sb;

    [MenuItem("Layout Editor/诊断/bundle 资产与 OBJ 子资产审计")]
    public static void RunFromMenu()
    {
        Run();
    }

    private static void Run()
    {
        sb = new StringBuilder();
        try { AuditBundles(); }
        catch (Exception e) { sb.AppendLine("BUNDLE EXCEPTION: " + e.Message); }
        try { AuditMeshes(); }
        catch (Exception e) { sb.AppendLine("MESH EXCEPTION: " + e.Message); }
        try { AuditScene(); }
        catch (Exception e) { sb.AppendLine("SCENE EXCEPTION: " + e.Message); }
        try
        {
            Directory.CreateDirectory("logs");
            File.WriteAllText(ResultPath, sb.ToString());
        }
        catch (Exception e) { Debug.LogError("[AUDIT] write failed: " + e.Message); }
        Debug.Log("[AUDIT] done -> " + ResultPath);
    }

    // ---------- A. bundle 资产逐个加载 ----------
    private static void AuditBundles()
    {
        sb.AppendLine("=== A. BUNDLE AUDIT ===");
        for (int b = 0; b < BundleNames.Length; b++)
        {
            string name = BundleNames[b];
            string path = Path.Combine(Application.streamingAssetsPath, "Windows/" + name);
            sb.AppendLine("[bundle] " + name + " path=" + path + " exists=" + File.Exists(path));
            Debug.Log("[AUDIT] bundle-load-begin " + name);
            AssetBundle bundle = AssetBundle.LoadFromFile(path);
            Debug.Log("[AUDIT] bundle-load-end   " + name);
            if (bundle == null)
            {
                sb.AppendLine("  LoadFromFile == null");
                continue;
            }
            string[] names = bundle.GetAllAssetNames();
            sb.AppendLine("  assets=" + names.Length);
            for (int i = 0; i < names.Length; i++)
            {
                Debug.Log("[AUDIT] asset-load-begin " + names[i]);
                try
                {
                    UnityEngine.Object obj = bundle.LoadAsset(names[i]);
                    Debug.Log("[AUDIT] asset-load-end   " + names[i] + " -> " + (obj == null ? "null" : obj.GetType().Name));
                    sb.AppendLine("  " + names[i] + " -> " + (obj == null ? "null" : obj.GetType().Name));
                }
                catch (Exception e)
                {
                    Debug.Log("[AUDIT] asset-load-throw " + names[i] + ": " + e.Message);
                    sb.AppendLine("  " + names[i] + " THROW " + e.Message);
                }
            }
            bundle.Unload(true);
        }
    }

    // ---------- B. OBJ 网格 sub-asset fileID ----------
    private static void AuditMeshes()
    {
        sb.AppendLine("=== B. OBJ MESH SUBASSETS ===");
        for (int i = 0; i < ObjPaths.Length; i++)
        {
            string p = ObjPaths[i];
            sb.AppendLine("[obj] " + p);
            UnityEngine.Object[] objs = AssetDatabase.LoadAllAssetsAtPath(p);
            sb.AppendLine("  subassets=" + objs.Length);
            for (int j = 0; j < objs.Length; j++)
            {
                UnityEngine.Object o = objs[j];
                string guid;
                long localId;
                bool ok = TryGetGuidAndLocalId(o, out guid, out localId);
                string extra = "";
                Mesh m = o as Mesh;
                if (m != null) extra = " verts=" + m.vertexCount + " sub=" + m.subMeshCount;
                sb.AppendLine("  " + (o == null ? "null" : o.GetType().Name + ":" + o.name)
                    + " localFileID=" + (ok ? localId.ToString() : "?") + extra);
            }
        }
    }

    // ---------- C. 场景实例渲染器审计 ----------
    private static void AuditScene()
    {
        sb.AppendLine("=== C. SCENE RENDERERS ===");
        Renderer[] renderers = UnityEngine.Object.FindObjectsOfType<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            Transform t = r.transform;
            string rootName = t.root != null ? t.root.name : "?";
            if (rootName != "web_kettle_hotwater" && rootName != "Switch"
                && t.gameObject.name != "web_kettle_hotwater" && t.gameObject.name != "Switch")
                continue;
            Material mat = r.sharedMaterial;
            string shaderInfo = "<null>";
            if (mat != null)
            {
                Shader sh = mat.shader;
                shaderInfo = sh == null ? "<null-shader>"
                    : (sh.name + " supported=" + sh.isSupported);
            }
            string meshInfo = "-";
            MeshFilter mf = r.GetComponent<MeshFilter>();
            if (mf != null)
                meshInfo = mf.sharedMesh == null ? "<null-mesh>" : ("mesh=" + mf.sharedMesh.name + " verts=" + mf.sharedMesh.vertexCount);
            MeshRenderer mr = r as MeshRenderer;
            SkinnedMeshRenderer smr = r as SkinnedMeshRenderer;
            if (smr != null && smr.sharedMesh != null)
                meshInfo = "mesh=" + smr.sharedMesh.name + " verts=" + smr.sharedMesh.vertexCount;
            string mats = "";
            if (mr != null)
            {
                Material[] ms = mr.sharedMaterials;
                mats = " mats=" + ms.Length;
                for (int k = 0; k < ms.Length; k++)
                    mats += "[" + (ms[k] == null ? "null" : ms[k].name) + "]";
            }
            sb.AppendLine("  " + r.GetType().Name + " go=" + r.gameObject.name + " root=" + rootName
                + " enabled=" + r.enabled + " activeInHierarchy=" + r.gameObject.activeInHierarchy
                + " shader=" + shaderInfo + " " + meshInfo + mats);
        }
    }

    private static bool TryGetGuidAndLocalId(UnityEngine.Object o, out string guid, out long localId)
    {
        guid = "";
        localId = 0;
        if (o == null)
            return false;
        try
        {
            MethodInfo mi = typeof(AssetDatabase).GetMethod(
                "TryGetGUIDAndLocalFileIdentifier",
                BindingFlags.Public | BindingFlags.Static);
            if (mi != null)
            {
                object[] args = new object[] { o, null, 0L };
                bool ok = (bool)mi.Invoke(null, args);
                if (ok)
                {
                    guid = args[1] as string;
                    localId = (long)args[2];
                    return true;
                }
            }
        }
        catch (Exception)
        {
        }
        guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(o));
        return !string.IsNullOrEmpty(guid);
    }
}
