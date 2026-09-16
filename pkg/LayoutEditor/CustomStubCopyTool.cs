using System;
using LevelEditorStub;
using UnityEditor;
using UnityEngine;

/// <summary>
/// CustomStub 配套资产维护（统一单程序集重构后精简）。
///
/// 母本 → 每集 stub 副本的拷贝分发链已废除（统一单程序集 WebCustomStubRuntime
/// 取代 Stub_&lt;set&gt; 每集编译）。本类仅保留 RandomDispenser 包装 prefab 的幂等生成
/// （随机食材箱专属道具：基于 common01 Dispenser 复制 + 挂 SpecificPseudoPrefabTag
/// 空 RandomCrate| 载体 + 空 PseudoPrefabSOArray）。
/// </summary>
public static class CustomStubCopyTool
{
    public const string RandomDispenserPrefabPath = "Assets/commonW1/prefabs/core/counters/RandomDispenser.prefab";
    private const string BaseDispenserPrefabPath = "Assets/common01/prefabs/counters/Dispenser.prefab";

    /// <summary>幂等生成 RandomDispenser 包装 prefab（已存在则直接返回）。</summary>
    public static void EnsureRandomDispenserPrefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(RandomDispenserPrefabPath);
        if (existing != null)
            return;
        try
        {
            EnsureFolder("Assets/commonW1/prefabs/core/counters");
            if (!AssetDatabase.CopyAsset(BaseDispenserPrefabPath, RandomDispenserPrefabPath))
            {
                Debug.LogWarning("[CustomStub] RandomDispenser 复制失败: " + BaseDispenserPrefabPath);
                return;
            }
            AssetDatabase.SaveAssets();
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(RandomDispenserPrefabPath);
            if (go == null)
                return;
            go.name = "RandomDispenser";
            var tag = go.GetComponent<SpecificPseudoPrefabTag>();
            if (tag == null)
                tag = go.AddComponent<SpecificPseudoPrefabTag>();
            tag.prefabTag = "RandomCrate|";
            var soArray = go.GetComponent<PseudoPrefabSOArray>();
            if (soArray == null)
                soArray = go.AddComponent<PseudoPrefabSOArray>();
            soArray.pseudoPrefabSOs = new PseudoPrefabSO[0];
            EditorUtility.SetDirty(go);
            AssetDatabase.SaveAssets();
            Debug.Log("[CustomStub] 已生成 RandomDispenser 包装 prefab: " + RandomDispenserPrefabPath);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[CustomStub] RandomDispenser 生成异常: " + ex.Message);
        }
    }

    private static void EnsureFolder(string folderPath)
    {
        var segments = folderPath.Split('/');
        var current = segments[0];
        for (int i = 1; i < segments.Length; i++)
        {
            var next = current + "/" + segments[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, segments[i]);
            current = next;
        }
    }
}
