using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using LevelEditor;
using LevelEditorStub;

/// <summary>
/// 管理器环境 rig 错挂物品组件修复（2026-09-28 test_juice「Play 进图即 NRE」事故）。
///
/// 症状：PseudoPrefabManager.OnEnable → Init → ResetAllPseudoPrefabs 在
/// PseudoPrefabManager.LoadAsset NRE（PseudoPrefabManager.cs:343），异常一路
/// 上抛把整个初始化【中断】——其后全部伪预制件子物体未创建；同步期
/// PseudoPrefabCleanPlateStack.SetupAfterStartSynchronising 再因 childGameObject
/// 未赋值连锁 NRE。
///
/// 根因（场景数据损坏）：管理器环境 rig（CampaignGameEnvironment 下的
/// CheatManager/RatManager 等宿主管理器物体）被钉上了物品组件对
/// （PseudoPrefab 派生运行时组件 + PseudoPrefabStub 派生 stub）。rig 物体没有
/// 基础 stub，ApplyStub 补挂派生组件时 pseudoPrefabSO 只能留空 → 首次
/// ResetChild 的 LoadAsset(null) 即 NRE。
///
/// 处置：遍历 rig（PseudoPrefabManagerStub 引用的全部管理器物体的层级根），
/// 删除 rig 内一切 PseudoPrefab / PseudoPrefabStub 家族组件（管理器本体均非
/// 这两个家族——PseudoPrefabManagerStub 继承 MonoBehaviour，不受影响）。
/// 配套防线（防再犯）：LayoutEditorStubIO.ApplyStub 入口守卫 + SceneLayoutApplier
/// 物品匹配守卫（LayoutEditorStubIO.IsUnderManagerEnvironment）。
/// </summary>
public static class LayoutEditorManagerRigRepair
{
    [MenuItem("Layout Editor/清理管理器环境错挂的物品组件（Play Init NRE）", false, 204)]
    public static void Run()
    {
        try
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                EditorUtility.DisplayDialog("管理器环境修复", "没有打开中的场景：请先打开要修复的关卡场景再运行。", "好");
                return;
            }

            // rig 根集合：管理器 stub 引用的全部管理器物体的层级根（去重）。
            var roots = new List<Transform>();
            var rootIds = new HashSet<int>();
            Action<Transform> addRoot = t =>
            {
                if (t == null) return;
                var root = t.root;
                if (root != null && rootIds.Add(root.GetInstanceID()))
                    roots.Add(root);
            };
            foreach (var stub in UnityEngine.Object.FindObjectsOfType<PseudoPrefabManagerStub>())
            {
                if (stub == null) continue;
                addRoot(stub.transform);
                var fields = stub.GetType().GetFields(System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.Instance);
                foreach (var f in fields)
                {
                    if (f.FieldType != typeof(GameObject)) continue;
                    var go = f.GetValue(stub) as GameObject;
                    if (go != null) addRoot(go.transform);
                }
            }
            if (roots.Count == 0)
            {
                EditorUtility.DisplayDialog("管理器环境修复",
                    "场景里没有 PseudoPrefabManagerStub（非关卡场景？），无需修复。", "好");
                return;
            }

            // 收集 rig 内的物品家族组件
            var doomed = new List<Component>();
            var detail = new StringBuilder();
            foreach (var root in roots)
            {
                var stack = new Stack<Transform>();
                stack.Push(root);
                while (stack.Count > 0)
                {
                    var t = stack.Pop();
                    for (int i = 0; i < t.childCount; i++)
                        stack.Push(t.GetChild(i));
                    foreach (var c in t.GetComponents<Component>())
                    {
                        if (c == null) continue; // Missing Script 由孤儿修复工具处理
                        if (c is PseudoPrefab || c is PseudoPrefabStub)
                        {
                            doomed.Add(c);
                            detail.Append(t.name).Append(" ← ").Append(c.GetType().Name).Append('\n');
                        }
                    }
                }
            }

            if (doomed.Count == 0)
            {
                EditorUtility.DisplayDialog("管理器环境修复",
                    "扫描完毕：" + roots.Count + " 个 rig 根下没有发现错挂的物品组件，场景是干净的。", "好");
                return;
            }

            LayoutEditorLog.Log("[管理器环境修复] 将删除以下错挂组件（" + doomed.Count + " 个）:\n" + detail);
            var ok = EditorUtility.DisplayDialog("管理器环境修复",
                "在管理器环境 rig 内发现 " + doomed.Count + " 个错挂的物品组件（详见 Console 日志）。\n" +
                "这些组件会让 Play 进图即 NRE（初始化中断）。\n\n立即删除并保存场景？", "删除并保存", "取消");
            if (!ok) return;

            foreach (var c in doomed)
            {
                if (c != null)
                    Undo.DestroyObjectImmediate(c);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            LayoutEditorLog.Log("[管理器环境修复] 已删除 " + doomed.Count +
                " 个错挂组件并保存场景 " + scene.name + "（可 Ctrl+Z 撤销，撤销后请勿保存）");
            EditorUtility.DisplayDialog("管理器环境修复",
                "已删除 " + doomed.Count + " 个错挂组件并保存场景。\n现在 Play 应能正常进图。", "好");
        }
        catch (Exception ex)
        {
            LayoutEditorLog.LogWarning("[管理器环境修复] 异常: " + ex);
            EditorUtility.DisplayDialog("管理器环境修复", "修复异常：" + ex.Message, "好");
        }
    }
}
