using System;
using System.Reflection;
using LevelEditor;
using LevelEditorStub;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 热水器接水显示复现：按 Play 同源链路组装 testhotwt 的菜谱匹配表，
/// 模拟「马克杯 + 热水」的 GetOrderPlatingPrefab 判定，找出显示错模型的原因。
/// 菜单：Layout Editor/诊断/热水器接水显示复现
/// </summary>
public static class DiagKettlePourRepro
{
    [MenuItem("Layout Editor/诊断/热水器接水显示复现")]
    public static void Run()
    {
        var sb = new System.Text.StringBuilder();
        try
        {
            DoRun(sb);
        }
        catch (Exception e)
        {
            sb.AppendLine("EXCEPTION: " + e);
        }
        System.IO.Directory.CreateDirectory("logs");
        System.IO.File.WriteAllText("logs/kettle_pour_repro.txt", sb.ToString());
        Debug.Log(sb.ToString());
    }

    private static void DoRun(System.Text.StringBuilder sb)
    {
        var scene = EditorSceneManager.OpenScene(
            "Assets/LevelSets/tester_web/scenes/testhotwt.unity");
        sb.AppendLine("scene: " + scene.name);
        var mgr = UnityEngine.Object.FindObjectOfType<PseudoPrefabManager>();
        if (mgr == null) { sb.AppendLine("no PseudoPrefabManager"); return; }
        var stub = mgr.stub;
        mgr.Init();
        sb.AppendLine("manager init ok; levelInfo=" +
            (stub != null && stub.levelInfo != null ? stub.levelInfo.name : "?"));

        // ---- 组装关卡配置（与 Play 同源）----
        CampaignLevelConfig cfg = LevelConfigSetup.SetupConfig(
            stub.configTemplateSO, stub.levelInfo, 4);
        sb.AppendLine("config: " + cfg.name);

        // ---- 匹配表展开（复刻 GameUtils.GetAllOrderNodes）----
        System.Collections.Generic.List<OrderDefinitionNode> nodes =
            new System.Collections.Generic.List<OrderDefinitionNode>();
        nodes.AddRange(cfg.m_recipeMatchingList.m_recipes);
        for (int i = 0; i < cfg.m_recipeMatchingList.m_includeLists.Length; i++)
            nodes.AddRange(cfg.m_recipeMatchingList.m_includeLists[i].m_recipes);
        sb.AppendLine("match nodes: " + nodes.Count);

        // ---- 三种候选装盘步 ----
        PlatingStepData localMug = AssetDatabase.LoadAssetAtPath<PlatingStepData>(
            "Assets/common04/_supplies/PlatingStepData/Mug.asset");
        PlatingStepData bundleMug = PseudoPrefabManager.LoadAsset<PlatingStepData>(
            LoadSo("Assets/common03/food/PlatingSteps/Mug.asset"));
        sb.AppendLine("localMug=" + (localMug == null ? "<null>" : localMug.name + "#u" + localMug.m_uID) +
            " bundleMug=" + (bundleMug == null ? "<null>" : bundleMug.name + "#u" + bundleMug.m_uID) +
            " same=" + object.ReferenceEquals(localMug, bundleMug));

        // ---- 杯中内容 = 热水 ----
        var hotWaterNode = AssetDatabase.LoadAssetAtPath<OrderDefinitionNode>(
            "Assets/commonW5/ingredients/hotwater/Node_HotWater.asset");
        sb.AppendLine("hotwater node: " + (hotWaterNode == null ? "<null>" :
            hotWaterNode.name + "#u" + hotWaterNode.m_uID +
            " plating=" + FieldOf(hotWaterNode, "m_platingStep") +
            " platingPrefab=" + FieldOf(hotWaterNode, "m_platingPrefab")));
        AssembledDefinitionNode content = new IngredientAssembledNode(
            hotWaterNode as IngredientOrderNode);

        // ---- 逐节点匹配（复刻 GetOrderPlatingPrefab 主循环）----
        foreach (PlatingStepData plating in new PlatingStepData[] { localMug, bundleMug })
        {
            if (plating == null) continue;
            sb.AppendLine("== plating " + plating.name + " (u" + plating.m_uID + ")");
            for (int i = 0; i < nodes.Count; i++)
            {
                OrderDefinitionNode n = nodes[i];
                if (n == null) { sb.AppendLine("  [" + i + "] <null node>"); continue; }
                string desc = "  [" + i + "] " + n.name + " u" + n.m_uID +
                    " plating=" + FieldOf(n, "m_platingStep") +
                    " model=" + FieldOf(n, "m_platingPrefab") +
                    " comp=" + FieldOf(n, "m_composition");
                bool match = object.ReferenceEquals(n.m_platingStep, plating) &&
                    AssembledDefinitionNode.MatchingAlreadySimple(content.Simpilfy(), n.Simpilfy());
                if (match) desc += "  <<< MATCH";
                sb.AppendLine(desc);
            }
        }
    }

    private static string FieldOf(OrderDefinitionNode node, string field)
    {
        if (node == null) return "?";
        FieldInfo f = node.GetType().GetField(field,
            BindingFlags.Public | BindingFlags.Instance);
        if (f == null) return "-";
        object v;
        try { v = f.GetValue(node); }
        catch { return "err"; }
        if (v == null) return "null";
        if (v is GameObject) { GameObject g = (GameObject)v; return g == null ? "<fake-null>" : g.name; }
        if (v is PlatingStepData) return ((PlatingStepData)v).name + "#u" + ((PlatingStepData)v).m_uID;
        if (v is Array)
        {
            Array a = (Array)v;
            System.Text.StringBuilder b = new System.Text.StringBuilder("[");
            for (int i = 0; i < a.Length; i++)
            {
                if (i > 0) b.Append(",");
                object e = a.GetValue(i);
                b.Append(e is OrderDefinitionNode
                    ? ((OrderDefinitionNode)e).name + "#u" + ((OrderDefinitionNode)e).m_uID
                    : Convert.ToString(e));
            }
            b.Append("]");
            return b.ToString();
        }
        return v.ToString();
    }

    private static PseudoPrefabSO LoadSo(string path)
    {
        return AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(path);
    }
}
