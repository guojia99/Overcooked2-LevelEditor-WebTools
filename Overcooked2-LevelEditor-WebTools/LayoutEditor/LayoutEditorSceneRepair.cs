using System.Collections.Generic;
using LevelEditorStub;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 场景自愈：移除源预制件已缺失（guid 无法解析）的预制件实例，
/// 并修复 pseudoPrefabSO 被置空（fileID 0）的实例（从源预制件恢复）。
/// 宿主 PseudoPrefabManager.Init → ResetAllPseudoPrefabs 会遍历场景全部 PseudoPrefab，
/// 若某实例的源预制件缺失或 stub.pseudoPrefabSO 为空会抛 NullReferenceException，
/// 这里在插件侧修复（不改宿主文件）。
/// </summary>
public static class LayoutEditorSceneRepair
{
    /// <summary>清理当前活动场景：移除源预制件缺失的实例、恢复被置空的 pseudoPrefabSO。
    ///  返回移除的损坏实例数量。</summary>
    public static int RemoveBrokenPrefabInstances()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid())
            return 0;

        var all = new List<GameObject>();
        foreach (var root in scene.GetRootGameObjects())
            Collect(root.transform, all);

        var removed = 0;
        var restored = 0;
        foreach (var go in all)
        {
            var type = PrefabUtility.GetPrefabType(go);
            if (type != PrefabType.PrefabInstance && type != PrefabType.MissingPrefabInstance)
                continue;
            var srcObj = PrefabUtility.GetPrefabParent(go);
            if (srcObj == null)
            {
                Debug.Log("[LayoutEditor] 移除损坏的预制件实例（源预制件缺失）: " + go.name);
                Object.DestroyImmediate(go);
                removed++;
                continue;
            }
            var src = srcObj as GameObject;
            if (src == null)
            {
                Debug.Log("[LayoutEditor] 移除损坏的预制件实例（源预制件类型异常）: " + go.name);
                Object.DestroyImmediate(go);
                removed++;
                continue;
            }

            // 修复 pseudoPrefabSO 被置空：从源预制件恢复（宿主 ResetChild 会因此不抛空引用）
            var stub = go.GetComponent<PseudoPrefabStub>();
            if (stub != null && stub.pseudoPrefabSO == null)
            {
                var srcStub = src.GetComponent<PseudoPrefabStub>();
                if (srcStub != null && srcStub.pseudoPrefabSO != null)
                {
                    Undo.RecordObject(stub, "Layout Editor Repair PseudoPrefabSO");
                    stub.pseudoPrefabSO = srcStub.pseudoPrefabSO;
                    restored++;
                    Debug.Log("[LayoutEditor] 恢复 pseudoPrefabSO: " + go.name + " -> " + srcStub.pseudoPrefabSO.name);
                }
                else
                {
                    Debug.Log("[LayoutEditor] 移除损坏的预制件实例（pseudoPrefabSO 为空且无法恢复）: " + go.name);
                    Object.DestroyImmediate(go);
                    removed++;
                }
            }
        }

        if (removed > 0 || restored > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        return removed;
    }

    private static void Collect(Transform t, List<GameObject> outList)
    {
        if (t == null)
            return;
        outList.Add(t.gameObject);
        foreach (Transform child in t)
            Collect(child, outList);
    }
}
