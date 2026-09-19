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

    /// <summary>幂等生成老鼠偷食材包装 prefab（已存在则直接返回）。
    /// 全新壳（无现成宿主可复制）：PseudoPrefabStub 指向 bundle47 复古老鼠 triple
    /// （编辑器 Play 由 PseudoPrefabManager / 真机由 OC2DIYLevel 在实体扫描前
    /// 生成真实老鼠；bundle 通道全坏时 CustomStub.RatHeist 自行兜底实例化）
    /// + SpecificPseudoPrefabTag "RatHeist|" 空载体（运行时自愈挂组件）。
    /// 不加 collider：藏身工作台格的物理已由台面承担，壳不可见也不可碰。
    /// 目录 prefabs/mechanisms/ → catalog 自动归入「核心 · 机关」分组。</summary>
    public const string RatHeistPrefabPath = "Assets/commonW1/prefabs/mechanisms/RatHeist.prefab";
    public const string RatHeistSoPath = "Assets/commonW1/pseudo_prefab_so/special/RatHeistRatSO.asset";

    public static void EnsureRatHeistPrefab()
    {
        try
        {
            // 1. PseudoPrefabSO（bundle47 复古老鼠三元组）
            var so = AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(RatHeistSoPath);
            if (so == null)
            {
                EnsureFolder("Assets/commonW1/pseudo_prefab_so/special");
                so = ScriptableObject.CreateInstance<PseudoPrefabSO>();
                so.prefabName = "rat";
                so.bundleName = "bundle47";
                so.assetPath = "assets/prefabs/overcooked_legacy/beings/rat.prefab";
                AssetDatabase.CreateAsset(so, RatHeistSoPath);
                AssetDatabase.SaveAssets();
                Debug.Log("[CustomStub] 已生成老鼠 PseudoPrefabSO: " + RatHeistSoPath);
            }

            // 2. 包装 prefab（全新壳）
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(RatHeistPrefabPath);
            if (existing != null)
            {
                // SO 引用自愈（prefab 在而 SO 重建过 guid 变化）
                var stub = existing.GetComponent<PseudoPrefabStub>();
                if (stub != null && stub.pseudoPrefabSO == null)
                {
                    stub.pseudoPrefabSO = so;
                    EditorUtility.SetDirty(existing);
                    AssetDatabase.SaveAssets();
                }
                return;
            }
            EnsureFolder("Assets/commonW1/prefabs/mechanisms");
            var go = new GameObject("RatHeist");
            try
            {
                var stub = go.AddComponent<PseudoPrefabStub>();
                stub.pseudoPrefabSO = so;
                var tag = go.AddComponent<SpecificPseudoPrefabTag>();
                tag.prefabTag = "RatHeist|";
                PrefabUtility.CreatePrefab(RatHeistPrefabPath, go);
                Debug.Log("[CustomStub] 已生成 RatHeist 包装 prefab: " + RatHeistPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[CustomStub] RatHeist 生成异常: " + ex.Message);
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
