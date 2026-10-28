using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using LevelEditor;
using LevelEditorStub;

/// <summary>
/// commonW5 热水器 / 核心 Switch 资产审计（菜单手动触发，C# 4 兼容）。
///
/// 背景：AYCE 热水器素材必须全部本地化，不能走 bundle；核心 Switch 则仍走
/// common01 的 bundle 包装（材质 bundle38、shader bundle88）。本工具一次性输出：
///  - 热水器 prefab / OBJ 子资产 / 材质 / shader / 控制器 的加载状态；
///  - 热水器实例的 MeshFilter.sharedMesh、Renderer 材质与 shader；
///  - Switch 伪 prefab child 的材质 shader 与 isSupported；
///  - Switch active/inactive PseudoPrefabSO 的 bundle/assetPath 与加载结果；
///  - 当前已加载 bundle 是否包含 bundle38 / bundle88。
/// 结果写 logs/commonw5_kettle_switch_audit.txt，便于在 VM 里回传定位。
/// </summary>
public static class DiagCommonW5Audit
{
    private const string ResultPath = "logs/commonw5_kettle_switch_audit.txt";
    private const string KettlePrefab = "Assets/commonW5/prefabs/kettle/web_kettle_hotwater.prefab";

    private static readonly string[] ObjPaths = new string[]
    {
        "Assets/commonW5/kettle/meshes/m_kettle_anniversary_01.obj",
        "Assets/commonW5/kettle/meshes/m_sk_cooker_anniversary_01.obj",
        "Assets/commonW5/kettle/meshes/m_dlc100_teacup_cup.obj",
        "Assets/commonW5/kettle/meshes/m_dlc100_teacup_drink_surface.obj",
        "Assets/commonW5/kettle/meshes/m_dlc100_teacup_spoon.obj",
    };

    private static readonly string[] ShaderPaths = new string[]
    {
        "Assets/commonW5/kettle/shaders/WebKettleEmissive.shader",
        "Assets/commonW5/kettle/shaders/WebOpaqueDiffuse.shader",
        "Assets/commonW5/kettle/shaders/WebLiquid.shader",
        "Assets/commonW5/kettle/shaders/WebPfxAlpha.shader",
        "Assets/commonW5/kettle/shaders/WebPfxAdditive.shader",
    };

    private static readonly string[] AnimPaths = new string[]
    {
        "Assets/commonW5/kettle/animations/DLC0100_Kettle.controller",
        "Assets/commonW5/kettle/animations/KettleCold.anim",
        "Assets/commonW5/kettle/animations/KettleHeating.anim",
        "Assets/commonW5/kettle/animations/KettleHot.anim",
    };

    private static readonly string[] MaterialPaths = new string[]
    {
        "Assets/commonW5/kettle/materials/mat_Kettle_Anniversary_01.mat",
        "Assets/commonW5/kettle/materials/mat_Birthday_TeaCup_01_6.mat",
        "Assets/commonW5/kettle/materials/mat_web_cooker.mat",
        "Assets/commonW5/kettle/materials/mat_web_teacup.mat",
        "Assets/commonW5/kettle/materials/mat_web_pfx_fire.mat",
        "Assets/commonW5/kettle/materials/mat_web_pfx_steam.mat",
        "Assets/commonW5/kettle/materials/mat_web_pfx_drip.mat",
        "Assets/commonW5/kettle/materials/mat_web_liquid_water.mat",
        "Assets/commonW5/kettle/materials/mat_web_liquid_milk.mat",
        "Assets/commonW5/kettle/materials/mat_web_liquid_milktea.mat",
        "Assets/commonW5/kettle/materials/mat_web_liquid_honeytea.mat",
    };

    private static StringBuilder sb;

    [MenuItem("Layout Editor/诊断/热水器粒子与完成态审计")]
    public static void KettlePfxAudit()
    {
        var sb = new System.Text.StringBuilder();
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(KettlePrefab);
        if (go == null) { Debug.LogError("kettle prefab load failed"); return; }
        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            sb.AppendLine(ps.gameObject.name + ": lifetime=" +
                main.startLifetime.constantMin + "~" + main.startLifetime.constantMax +
                " speed=" + main.startSpeed.constantMin + "~" + main.startSpeed.constantMax +
                " size=" + main.startSize.constantMin + "~" + main.startSize.constantMax +
                " gravity=" + main.gravityModifier.constant +
                " maxParticles=" + main.maxParticles);
        }
        var anim = go.GetComponent<Animator>();
        sb.AppendLine("animator=" + (anim == null ? "<null>" : (anim.runtimeAnimatorController != null
            ? anim.runtimeAnimatorController.name : "no controller")));
        foreach (Transform c in go.transform)
            sb.AppendLine("child " + c.name + " active=" + c.gameObject.activeSelf);
        string logPath = "logs/kettle_pfx_audit.txt";
        System.IO.File.WriteAllText(logPath, sb.ToString());
        Debug.Log(sb.ToString());
        EditorUtility.DisplayDialog("热水器粒子审计", "已写入 " + logPath, "OK");
    }

    [MenuItem("Layout Editor/诊断/强制 Reimport commonW5 热水器资产")]
    public static void ForceReimport()
    {
        AssetDatabase.Refresh();
        string[] paths = new string[]
        {
            KettlePrefab,
            "Assets/commonW5/prefabs/teacups/web_teacup.prefab",
            "Assets/commonW5/prefabs/teacups/web_teacup_stack_clean.prefab",
            "Assets/commonW5/prefabs/teacups/web_teacup_dirty.prefab",
            "Assets/commonW5/prefabs/teacups/web_teacup_stack_dirty.prefab",
            "Assets/commonW5/prefabs/counters/web_sink_teacup.prefab",
            "Assets/commonW5/prefabs/counters/web_teacup_return.prefab",
            "Assets/commonW5/pseudo_prefab_so/utensils/TeacupStackCleanSO.asset",
            "Assets/commonW5/teacup/meshes/m_sk_sink_dlc100.obj",
            "Assets/commonW5/teacup/meshes/m_sk_plate_return_01.obj",
            "Assets/commonW5/teacup/meshes/m_dlc2_glassdirt_01.obj",
            "Assets/commonW5/teacup/materials/mat_web_sink_wood.mat",
            "Assets/commonW5/teacup/materials/mat_web_plate_return.mat",
            "Assets/commonW5/teacup/materials/mat_web_glassdirt.mat",
            "Assets/commonW5/teacup/materials/mat_web_sink_water.mat",
            "Assets/commonW5/custom_recipes/milk_tea/models/Web_TeaContents_Water.prefab",
            "Assets/commonW5/custom_recipes/milk_tea/models/Web_TeaContents_Tea.prefab",
            "Assets/commonW5/custom_recipes/milk_tea/models/Web_TeaContents_MilkTea.prefab",
            "Assets/commonW5/custom_recipes/milk_tea/models/Web_TeaContents_HoneyTea.prefab",
            "Assets/commonW5/custom_recipes/milk_tea/models/Web_TeaContents_HoneyMilkTea.prefab",
            "Assets/commonW5/ingredients/hotwater/Node_HotWater.asset",
            "Assets/commonW5/pseudo_prefab_so/food/HotWaterSO.asset",
            "Assets/commonW5/kettle/animations/DLC0100_Kettle.controller",
            "Assets/commonW5/kettle/animations/KettleCold.anim",
            "Assets/commonW5/kettle/animations/KettleHeating.anim",
            "Assets/commonW5/kettle/animations/KettleHot.anim",
        };
        for (int i = 0; i < ObjPaths.Length; i++)
            AssetDatabase.ImportAsset(ObjPaths[i], ImportAssetOptions.ForceUpdate);
        for (int i = 0; i < ShaderPaths.Length; i++)
            AssetDatabase.ImportAsset(ShaderPaths[i], ImportAssetOptions.ForceUpdate);
        for (int i = 0; i < MaterialPaths.Length; i++)
            AssetDatabase.ImportAsset(MaterialPaths[i], ImportAssetOptions.ForceUpdate);
        for (int i = 0; i < paths.Length; i++)
        {
            if (File.Exists(paths[i]))
                AssetDatabase.ImportAsset(paths[i], ImportAssetOptions.ForceUpdate);
        }
        string[] guids = AssetDatabase.FindAssets("",
            new string[] { "Assets/commonW5/custom_recipes/milk_tea" });
        for (int i = 0; i < guids.Length; i++)
        {
            string p = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (!string.IsNullOrEmpty(p))
                AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
        }
        AssetDatabase.Refresh();
        Debug.Log("[CW5-AUDIT] commonW5 热水器资产已强制重导入；建议重开场景或运行「重建伪 prefab child」。");
    }

    [MenuItem("Layout Editor/诊断/commonW5 热水器与开关审计")]
    public static void Run()
    {
        sb = new StringBuilder();
        try
        {
            DumpAssets();
            DumpKettleInstances();
            DumpSwitches();
            DumpBundles();
        }
        catch (Exception e)
        {
            sb.AppendLine("EXCEPTION: " + e);
            Debug.LogError("[CW5-AUDIT] " + e);
        }
        try
        {
            Directory.CreateDirectory("logs");
            File.WriteAllText(ResultPath, sb.ToString());
        }
        catch (Exception e)
        {
            Debug.LogError("[CW5-AUDIT] write failed: " + e.Message);
        }
        Debug.Log("[CW5-AUDIT] done -> " + ResultPath);
    }

    private static void DumpAssets()
    {
        sb.AppendLine("=== A. LOCAL ASSETS ===");
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(KettlePrefab);
        sb.AppendLine("prefab=" + KettlePrefab + " loaded=" + (prefab != null));
        if (prefab != null)
            DumpPrefab(prefab);

        for (int i = 0; i < ObjPaths.Length; i++)
        {
            string path = ObjPaths[i];
            UnityEngine.Object[] objs = AssetDatabase.LoadAllAssetsAtPath(path);
            sb.AppendLine("[obj] " + path + " subassets=" + objs.Length);
            for (int j = 0; j < objs.Length; j++)
            {
                UnityEngine.Object o = objs[j];
                string guid;
                long localId;
                bool ok = TryGetGuidAndLocalId(o, out guid, out localId);
                Mesh mesh = o as Mesh;
                sb.AppendLine("  " + (o == null ? "<null>" : o.GetType().Name + ":" + o.name)
                    + " fileID=" + (ok ? localId.ToString() : "?")
                    + (mesh != null ? " verts=" + mesh.vertexCount + " sub=" + mesh.subMeshCount : ""));
            }
        }

        for (int i = 0; i < ShaderPaths.Length; i++)
        {
            Shader sh = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPaths[i]);
            sb.AppendLine("[shader] " + ShaderPaths[i] + " loaded=" + (sh != null)
                + (sh != null ? " name=" + sh.name + " supported=" + sh.isSupported : ""));
        }

        for (int i = 0; i < MaterialPaths.Length; i++)
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>(MaterialPaths[i]);
            sb.AppendLine("[mat] " + MaterialPaths[i] + " loaded=" + (m != null));
            if (m != null)
                DumpMaterial(m, "  ");
        }

        for (int i = 0; i < AnimPaths.Length; i++)
        {
            string path = AnimPaths[i];
            if (path.EndsWith(".anim", StringComparison.OrdinalIgnoreCase))
            {
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                sb.AppendLine("[anim] " + path + " loaded=" + (clip != null)
                    + (clip != null ? " length=" + clip.length : ""));
            }
            else
            {
                RuntimeAnimatorController ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(path);
                sb.AppendLine("[controller] " + path + " loaded=" + (ctrl != null));
            }
        }
    }

    private static void DumpPrefab(GameObject prefab)
    {
        GameObject inst = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (inst == null)
        {
            sb.AppendLine("  instantiate=<null>");
            return;
        }
        try
        {
            DumpRenderers(inst, "  ");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(inst);
        }
    }

    private static void DumpKettleInstances()
    {
        sb.AppendLine("=== B. SCENE KETTLE ===");
        GameObject[] all = UnityEngine.Object.FindObjectsOfType<GameObject>();
        int found = 0;
        for (int i = 0; i < all.Length; i++)
        {
            GameObject go = all[i];
            if (go == null || go.name != "web_kettle_hotwater")
                continue;
            found++;
            sb.AppendLine("[instance] " + GetPath(go.transform));
            DumpRenderers(go, "  ");
            Component kettle = go.GetComponent("Kettle");
            sb.AppendLine("  Kettle component=" + (kettle != null));
            if (kettle != null)
            {
                var so = new SerializedObject(kettle);
                SerializedProperty sw = so.FindProperty("m_switch");
                sb.AppendLine("  m_switch=" + (sw != null && sw.objectReferenceValue != null
                    ? sw.objectReferenceValue.name : "<null>"));
            }
        }
        sb.AppendLine("kettle instances=" + found);
    }

    private static void DumpSwitches()
    {
        sb.AppendLine("=== C. SCENE SWITCHES ===");
        PseudoPrefab[] pseudos = UnityEngine.Object.FindObjectsOfType<PseudoPrefab>();
        int found = 0;
        for (int i = 0; i < pseudos.Length; i++)
        {
            PseudoPrefab pseudo = pseudos[i];
            if (pseudo == null)
                continue;
            PseudoPrefabSwitchStub stub = pseudo.GetComponent<PseudoPrefabSwitchStub>();
            if (stub == null)
                continue;
            found++;
            sb.AppendLine("[switch] " + GetPath(pseudo.transform)
                + " startEnabled=" + stub.startEnabled);
            DumpPseudoSo("  active", stub.activeMaterial);
            DumpPseudoSo("  inactive", stub.inactiveMaterial);
            if (pseudo.childGameObject == null)
            {
                sb.AppendLine("  child=<null>");
            }
            else
            {
                sb.AppendLine("  child=" + pseudo.childGameObject.name);
                DumpRenderers(pseudo.childGameObject, "  ");
            }
            TryLoadSoMaterial("  loaded-active", stub.activeMaterial);
            TryLoadSoMaterial("  loaded-inactive", stub.inactiveMaterial);
        }
        sb.AppendLine("switch wrappers=" + found);
    }

    private static void DumpPseudoSo(string label, PseudoPrefabSO so)
    {
        if (so == null)
        {
            sb.AppendLine(label + "=<null>");
            return;
        }
        sb.AppendLine(label + " name=" + so.prefabName + " bundle=" + so.bundleName
            + " assetPath=" + so.assetPath + " directAsset=" + (so.asset != null));
    }

    private static void TryLoadSoMaterial(string label, PseudoPrefabSO so)
    {
        if (so == null || PseudoPrefabManager.Instance == null)
            return;
        try
        {
            Material m = PseudoPrefabManager.LoadAsset<Material>(so);
            sb.AppendLine(label + " material=" + (m != null ? m.name : "<null>"));
            if (m != null)
                DumpMaterial(m, "    ");
        }
        catch (Exception e)
        {
            sb.AppendLine(label + " EXCEPTION " + e.Message);
        }
    }

    private static void DumpRenderers(GameObject root, string indent)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        sb.AppendLine(indent + "renderers=" + renderers.Length);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null)
                continue;
            string meshInfo = "-";
            MeshFilter mf = r.GetComponent<MeshFilter>();
            if (mf != null)
                meshInfo = mf.sharedMesh == null ? "<null-mesh>"
                    : "mesh=" + mf.sharedMesh.name + " verts=" + mf.sharedMesh.vertexCount;
            sb.AppendLine(indent + r.GetType().Name + " go=" + r.gameObject.name
                + " enabled=" + r.enabled + " active=" + r.gameObject.activeInHierarchy
                + " " + meshInfo);
            Material[] mats = r.sharedMaterials;
            for (int j = 0; j < mats.Length; j++)
            {
                if (mats[j] == null)
                {
                    sb.AppendLine(indent + "  mat[" + j + "]=<null>");
                    continue;
                }
                DumpMaterial(mats[j], indent + "  ");
            }
        }
    }

    private static void DumpMaterial(Material m, string indent)
    {
        string shaderName = "<null>";
        bool supported = false;
        if (m.shader != null)
        {
            shaderName = m.shader.name;
            supported = m.shader.isSupported;
        }
        sb.AppendLine(indent + "mat=" + m.name + " shader=" + shaderName
            + " supported=" + supported);
        if (m.shader == null)
            return;
        try
        {
            // Unity 2017 无 Material.GetTexturePropertyNames（2019.1+），用 ShaderUtil 枚举。
            int propCount = ShaderUtil.GetPropertyCount(m.shader);
            for (int i = 0; i < propCount; i++)
            {
                if (ShaderUtil.GetPropertyType(m.shader, i) != ShaderUtil.ShaderPropertyType.TexEnv)
                    continue;
                string slot = ShaderUtil.GetPropertyName(m.shader, i);
                Texture t = m.GetTexture(slot);
                if (t != null)
                    sb.AppendLine(indent + "  tex[" + slot + "]=" + t.name);
            }
        }
        catch (Exception)
        {
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

    private static void DumpBundles()
    {
        sb.AppendLine("=== D. LOADED BUNDLES ===");
        bool has38 = false;
        bool has88 = false;
        foreach (AssetBundle b in AssetBundle.GetAllLoadedAssetBundles())
        {
            if (b == null)
                continue;
            string n = b.name;
            if (n == "bundle38") has38 = true;
            if (n == "bundle88") has88 = true;
        }
        sb.AppendLine("bundle38=" + has38 + " bundle88=" + has88);
        sb.AppendLine("PseudoPrefabManager=" + (PseudoPrefabManager.Instance != null));
        if (PseudoPrefabManager.Instance != null && PseudoPrefabManager.Instance.stub != null
            && PseudoPrefabManager.Instance.stub.levelInfo != null)
        {
            string[] deps = PseudoPrefabManager.Instance.stub.levelInfo.dependencies;
            sb.AppendLine("levelInfo.dependencies=" + (deps == null ? "<null>" : string.Join(",", deps)));
        }
    }

    private static string GetPath(Transform t)
    {
        if (t == null)
            return "<null>";
        string path = t.name;
        Transform p = t.parent;
        while (p != null)
        {
            path = p.name + "/" + path;
            p = p.parent;
        }
        return path;
    }
}
