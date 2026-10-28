using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// kettle 实例化三条 "Unexpected node type." 归因诊断（一次性工具，C#4 兼容）。
/// 菜单手动触发（不再随域重载自动执行）：逐个加载 web_kettle_hotwater 的全部
/// 依赖资产，每步前后打 [DIAG] 标记；最后单独 InstantiatePrefab。三条错误落在
/// 哪两步之间，真凶即该步资产。结果写 logs/kettle_diag_result.txt。
/// </summary>
public static class DiagKettleInstantiate
{
    private const string KettlePath = "Assets/commonW5/prefabs/kettle/web_kettle_hotwater.prefab";
    private const string ResultPath = "logs/kettle_diag_result.txt";

    private static readonly string[] Steps = new string[]
    {
        "material:Assets/commonW5/kettle/materials/mat_Kettle_Anniversary_01.mat",
        "material:Assets/commonW5/kettle/materials/mat_web_cooker.mat",
        "material:Assets/commonW5/kettle/materials/mat_web_pfx_fire.mat",
        "material:Assets/commonW5/kettle/materials/mat_web_pfx_steam.mat",
        "material:Assets/commonW5/kettle/materials/mat_web_pfx_drip.mat",
        "shader:Assets/commonW5/kettle/shaders/WebKettleEmissive.shader",
        "shader:Assets/commonW5/kettle/shaders/WebOpaqueDiffuse.shader",
        "shader:Assets/commonW5/kettle/shaders/WebPfxAdditive.shader",
        "shader:Assets/commonW5/kettle/shaders/WebPfxAlpha.shader",
        "mesh:Assets/commonW5/kettle/meshes/m_kettle_anniversary_01.obj",
        "mesh:Assets/commonW5/kettle/meshes/m_sk_cooker_anniversary_01.obj",
        "anim:Assets/commonW5/kettle/animations/DLC0100_Kettle.controller",
        "anim:Assets/commonW5/kettle/animations/KettleCold.anim",
        "anim:Assets/commonW5/kettle/animations/KettleHeating.anim",
        "anim:Assets/commonW5/kettle/animations/KettleHot.anim",
        "audio:Assets/commonW5/kettle/audio/DLC_100_Kettle_Dispenser_Boil_01.ogg",
        "audio:Assets/commonW5/kettle/audio/DLC_100_Kettle_Dispenser_Pour_01.ogg",
        "asset:Assets/commonW5/ingredients/hotwater/Node_HotWater.asset",
    };

    private static StringBuilder sb;

    [MenuItem("Layout Editor/诊断/热水器实例化依赖审计")]
    public static void RunFromMenu()
    {
        Run();
    }

    private static void Step(string label, Action fn)
    {
        Debug.Log("[DIAG] step-begin " + label);
        try
        {
            fn();
        }
        catch (Exception e)
        {
            Debug.LogError("[DIAG] step-throw " + label + ": " + e.Message);
            sb.AppendLine("THROW " + label + ": " + e.Message);
        }
        Debug.Log("[DIAG] step-end   " + label);
        sb.AppendLine("OK " + label);
    }

    private static void Run()
    {
        sb = new StringBuilder();

        Step("prefab-load", delegate
        {
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(KettlePath);
            if (go == null) throw new Exception("prefab == null");
            Component[] comps = go.GetComponentsInChildren<Component>(true);
            string[] names = new string[comps.Length];
            for (int i = 0; i < comps.Length; i++)
                names[i] = comps[i] != null ? comps[i].GetType().Name : "<null>";
            sb.AppendLine("prefab components: " + string.Join(", ", names));
        });

        for (int i = 0; i < Steps.Length; i++)
        {
            string s = Steps[i];
            int colon = s.IndexOf(':');
            string label = s.Substring(0, colon);
            string path = s.Substring(colon + 1);
            Step(label + ":" + Path.GetFileName(path), delegate
            {
                System.Type type = typeof(UnityEngine.Object);
                if (label == "material") type = typeof(Material);
                else if (label == "shader") type = typeof(Shader);
                else if (label == "mesh") type = typeof(Mesh);
                else if (label == "audio") type = typeof(AudioClip);
                UnityEngine.Object loaded = AssetDatabase.LoadAssetAtPath(path, type);
                if (loaded == null) throw new Exception("load == null");
            });
        }

        Step("instantiate", delegate
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(KettlePath);
            GameObject inst = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (inst == null) throw new Exception("instance == null");
            sb.AppendLine("instantiate ok: " + inst.name);
            UnityEngine.Object.DestroyImmediate(inst);
        });

        Directory.CreateDirectory("logs");
        File.WriteAllText(ResultPath, sb.ToString());
        Debug.Log("[DIAG] done, result -> " + ResultPath);
    }
}
