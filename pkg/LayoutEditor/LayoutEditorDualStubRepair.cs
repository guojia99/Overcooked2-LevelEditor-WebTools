using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using LevelEditorStub;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 「双 stub」道具存量修复（2026-10-06 真机卡加载事故）。
///
/// 背景：wrapper 升级路径（酱料机/饮料机、上菜台/回收台变体等）补挂派生 stub 后
/// 未移除基础 PseudoPrefabStub + 基础 PseudoPrefab 组件对，且基础对在组件序前面。
/// 真机 OC2DIYLevel 按派生 stub 分类挂派生运行时组件，而 PseudoPrefab.Awake 的
/// GetComponent&lt;PseudoPrefabStub&gt;() 命中组件序第一的基础 stub →
/// PseudoPrefabDispenser.Setup 开头强转 (PseudoPrefabDispenserStub) 抛
/// InvalidCastException，中断 ResetAllPseudoPrefabs 全链 → 关卡永久卡加载
///（实测 diantang/s_jia_level_2_1 等含酱料机的图）。编辑器侧因基础 Setup 空操作
/// 而静默，从未暴露。
///
/// 处置：对「exact 基础 stub + 派生 stub 共存」的物体执行
/// LayoutEditorStubIO.PromoteDerivedStub——移除基础组件对、按派生 stub 补派生
/// 运行时、分发器生成食材为空时回落 soArray 首项。
///
/// 入口：
///  - 菜单（当前场景 / 全部关卡集批量）；
///  - 导出 prepare 自动修复（经 LayoutEditorSetExporter.DualStubCleanHook 钩子接入，
///    无弹窗，改写前逐场景备份到 writeback_history/dual_stub_repair/&lt;时间戳&gt;/）。
/// </summary>
[InitializeOnLoad]
public static class LayoutEditorDualStubRepair
{
    private const string BackupRoot = "writeback_history/dual_stub_repair";
    private const string LevelSetsRoot = "Assets/LevelSets";

    static LayoutEditorDualStubRepair()
    {
        // 导出 prepare 前自动修复——经 SetExporter.DualStubCleanHook 解耦接入，
        // 删除本文件后导出行为不变（钩子无订阅者）。
        LayoutEditorSetExporter.DualStubCleanHook += CleanScenesAuto;
    }

    // ==================== 菜单入口 ====================

    [MenuItem("Layout Editor/场景修复 (Scene Repair)/修复双 stub 道具 (Fix Dual-Stub Items · 真机卡加载)", false, 26)]
    private static void MenuRepairAllSets()
    {
        var scenes = CollectAllSetScenes();
        if (scenes.Count == 0)
        {
            EditorUtility.DisplayDialog("双 stub 修复", "Assets/LevelSets 下没有找到场景。", "确定");
            return;
        }
        var summary = "将扫描 " + scenes.Count + " 个关卡集场景，修复「基础 stub + 派生 stub 共存」的道具\n"
            + "（移除基础组件对并补派生运行时——2026-10-06 真机卡加载事故修复）。\n\n"
            + "改写的场景会先备份到 " + BackupRoot + "/<时间戳>/。开始？";
        if (!EditorUtility.DisplayDialog("双 stub 修复", summary, "开始", "取消"))
            return;
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var result = RepairScenes(scenes, stamp, true);
        EditorUtility.DisplayDialog("双 stub 修复", result, "确定");
    }

    [MenuItem("Layout Editor/场景修复 (Scene Repair)/修复双 stub 道具（仅当前场景）", false, 27)]
    private static void MenuRepairActiveScene()
    {
        var scene = SceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(scene.path) || !scene.path.StartsWith(LevelSetsRoot + "/", StringComparison.Ordinal))
        {
            EditorUtility.DisplayDialog("双 stub 修复", "请先打开一个关卡集场景（Assets/LevelSets/...）。", "确定");
            return;
        }
        var fixedNames = RepairOpenScene(scene);
        if (fixedNames.Count == 0)
        {
            EditorUtility.DisplayDialog("双 stub 修复", "当前场景没有双 stub 道具。", "确定");
            return;
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorUtility.DisplayDialog("双 stub 修复",
            "已修复 " + fixedNames.Count + " 个道具：\n" + string.Join("\n", fixedNames.ToArray())
            + "\n\n场景已保存（请 Ctrl+S 习惯勿丢——如需回滚用写回历史）。", "确定");
    }

    // ==================== 核心：场景级修复 ====================

    /// <summary>修复当前打开的场景（不保存——调用方决定）。返回被修复物体名列表。</summary>
    private static List<string> RepairOpenScene(Scene scene)
    {
        var fixedNames = new List<string>();
        var seen = new HashSet<GameObject>();
        // FindObjectsOfType 只回激活物体；双 stub 道具均为激活放置物。
        foreach (var stub in UnityEngine.Object.FindObjectsOfType<PseudoPrefabStub>())
        {
            if (stub == null)
                continue;
            var go = stub.gameObject;
            if (!seen.Add(go))
                continue;
            if (LayoutEditorStubIO.PromoteDerivedStub(go))
                fixedNames.Add(go.name);
        }
        return fixedNames;
    }

    /// <summary>批量修复场景列表（逐个打开→修复→保存→回开原场景）。返回汇总文案。</summary>
    private static string RepairScenes(List<string> scenePaths, string stamp, bool interactive)
    {
        var prevActive = EditorSceneManager.GetActiveScene().path;
        var totalFixed = 0;
        var edited = new List<string>();
        var detail = new StringBuilder();
        try
        {
            for (int i = 0; i < scenePaths.Count; i++)
            {
                if (interactive)
                    EditorUtility.DisplayProgressBar("双 stub 修复",
                        "场景 " + (i + 1) + "/" + scenePaths.Count + "：" + scenePaths[i], (float)i / scenePaths.Count);
                var scene = EditorSceneManager.OpenScene(scenePaths[i], OpenSceneMode.Single);
                var fixedNames = RepairOpenScene(scene);
                if (fixedNames.Count > 0)
                {
                    BackupSceneFile(scenePaths[i], stamp);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    totalFixed += fixedNames.Count;
                    edited.Add(scenePaths[i]);
                    detail.Append(scenePaths[i]).Append("：").Append(fixedNames.Count).Append(" 个（")
                        .Append(string.Join("、", fixedNames.ToArray())).Append("）\n");
                    Debug.Log("[DualStubRepair] " + scenePaths[i] + " 修复 " + fixedNames.Count + " 个: "
                        + string.Join("、", fixedNames.ToArray()));
                }
            }
        }
        finally
        {
            if (interactive)
                EditorUtility.ClearProgressBar();
            if (!string.IsNullOrEmpty(prevActive) && File.Exists(prevActive))
                EditorSceneManager.OpenScene(prevActive);
        }
        if (edited.Count > 0)
        {
            var reportDir = BackupRoot + "/" + stamp;
            if (!Directory.Exists(reportDir))
                Directory.CreateDirectory(reportDir);
            File.WriteAllText(reportDir + "/report.txt",
                "双 stub 修复报告 " + stamp + "\n修复物体 " + totalFixed + " 个，改写场景 " + edited.Count + " 个\n\n" + detail.ToString(),
                new UTF8Encoding(false));
        }
        return edited.Count > 0
            ? "共修复 " + totalFixed + " 个道具，改写 " + edited.Count + " 个场景。\n备份与报告: " + BackupRoot + "/" + stamp + "/"
            : "全部场景均无双 stub 道具，无需修复。";
    }

    /// <summary>导出 prepare 钩子（无弹窗）：只处理本次待导出场景。返回汇总（null=没有发现）。</summary>
    public static string CleanScenesAuto(List<string> scenePaths)
    {
        if (scenePaths == null || scenePaths.Count == 0)
            return null;
        // 快速预检（打开场景代价高）：YAML 文本同时含基础 stub 与任一派生 stub 脚本
        // guid 才进修复。预检漏判无碍——PromoteDerivedStub 幂等，无基础对直接跳过。
        var candidates = new List<string>();
        foreach (var p in scenePaths)
        {
            if (SceneMightHaveDualStub(p))
                candidates.Add(p);
        }
        if (candidates.Count == 0)
            return null;
        var stamp = "export_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var summary = RepairScenes(candidates, stamp, false);
        Debug.Log("[DualStubRepair] 导出前自动修复: " + summary);
        return "[DualStubRepair] " + summary;
    }

    /// <summary>场景 YAML 粗筛：同一文件出现基础 PseudoPrefabStub 与派生 stub 两种脚本 guid。
    ///  误报无害（打开后按组件实况处置），漏报由修复幂等性兜底。</summary>
    private static bool SceneMightHaveDualStub(string scenePath)
    {
        try
        {
            var abs = Path.GetFullPath(scenePath);
            if (!File.Exists(abs))
                return false;
            var text = File.ReadAllText(abs);
            if (string.IsNullOrEmpty(_baseStubGuid))
                _baseStubGuid = AssetDatabase.AssetPathToGUID(BaseStubScriptPath);
            if (string.IsNullOrEmpty(_baseStubGuid) || text.IndexOf(_baseStubGuid, StringComparison.Ordinal) < 0)
                return false;
            // 任一派生 stub 脚本 guid 出现即候选
            foreach (var pair in DerivedStubScriptPaths())
            {
                if (text.IndexOf(pair.Key, StringComparison.Ordinal) >= 0)
                    return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    private const string BaseStubScriptPath = "Assets/Scripts/LevelEditorStub/PseudoPrefabStub.cs";
    private static string _baseStubGuid;

    /// <summary>派生 stub 脚本 guid 前置表（场景粗筛用；新增 stub 类型时按需补充，
    ///  漏项只影响粗筛效率——PromoteDerivedStub 按类型实况处置，不依赖本表）。</summary>
    private static List<KeyValuePair<string, string>> _derivedStubGuids;

    private static List<KeyValuePair<string, string>> DerivedStubScriptPaths()
    {
        if (_derivedStubGuids != null)
            return _derivedStubGuids;
        _derivedStubGuids = new List<KeyValuePair<string, string>>();
        var names = new[]
        {
            "PseudoPrefabDispenserStub", "PseudoPrefabCookingUtensilStub", "PseudoPrefabServingStationStub",
            "PseudoPrefabPlateReturnStub", "PseudoPrefabCleanPlateStackStub", "PseudoPrefabTerminalStub",
            "PseudoPrefabTeleportalStub", "PseudoPrefabSwitchStub", "PseudoPrefabToggleSwitchStub",
            "PseudoPrefabPressureSwitchStub", "PseudoPrefabPlayerStub", "PseudoPrefabNPCStub",
            "PseudoPrefabAttachingFoodSpawnerStub", "PseudoPrefabConveyorStub", "PseudoPrefabTravelatorStub",
            "PseudoPrefabBurnerStub", "PseudoPrefabFlamethrowerStub", "PseudoPrefabHeatedOvenStub",
            "PseudoPrefabAutoWorkstationStub", "PseudoPrefabIngredientSprayStub", "PseudoPrefabMarkerStub",
            "PseudoPrefabMeshWithMaterialStub", "PseudoPrefabReplaceMaterialStub"
        };
        foreach (var n in names)
        {
            var guid = AssetDatabase.AssetPathToGUID("Assets/Scripts/LevelEditorStub/" + n + ".cs");
            if (!string.IsNullOrEmpty(guid))
                _derivedStubGuids.Add(new KeyValuePair<string, string>(guid, n));
        }
        return _derivedStubGuids;
    }

    // ==================== 工具 ====================

    private static List<string> CollectAllSetScenes()
    {
        var list = new List<string>();
        if (!AssetDatabase.IsValidFolder(LevelSetsRoot))
            return list;
        foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { LevelSetsRoot }))
        {
            var p = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(p) && p.EndsWith(".unity", StringComparison.Ordinal))
                list.Add(p);
        }
        list.Sort(StringComparer.Ordinal);
        return list;
    }

    /// <summary>改写场景前备份原文件（保留相对路径结构）。</summary>
    private static void BackupSceneFile(string scenePath, string stamp)
    {
        try
        {
            var src = Path.GetFullPath(scenePath);
            var dst = Path.GetFullPath(Path.Combine(BackupRoot + "/" + stamp, scenePath));
            var dstDir = Path.GetDirectoryName(dst);
            if (!string.IsNullOrEmpty(dstDir) && !Directory.Exists(dstDir))
                Directory.CreateDirectory(dstDir);
            File.Copy(src, dst, true);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[DualStubRepair] 场景备份失败（继续修复）: " + scenePath + " – " + e.Message);
        }
    }
}
