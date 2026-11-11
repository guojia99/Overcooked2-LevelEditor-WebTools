using System.Collections.Generic;
using LevelEditorStub;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 分料机输出字段迁移：2026-11 上游把 IngredientSplitter.outputPrefab(GameObject)
/// 改为 outputPrefabSO(PseudoPrefabSO)。旧场景/预制体里的 GameObject 引用由
/// FormerlySerializedAs 保存在 LegacyOutputPrefab，这里负责解析成 PseudoPrefabSO
/// 并清空 legacy，避免已有自定义分料机关卡静默失去输出。
/// </summary>
[InitializeOnLoad]
public static class LayoutEditorIngredientSplitterMigration
{
    private const string Menu = "Tools/Layout Editor/Repair IngredientSplitter Output SO";

    private static readonly Dictionary<int, PseudoPrefabSO> s_cache = new Dictionary<int, PseudoPrefabSO>();
    private static bool s_scanning;

    static LayoutEditorIngredientSplitterMigration()
    {
        EditorApplication.delayCall += MigrateOpenScene;
        EditorSceneManager.sceneOpened += OnSceneOpened;
    }

    private static void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, OpenSceneMode mode)
    {
        EditorApplication.delayCall += MigrateOpenScene;
    }

    public static void MigrateOpenScene()
    {
        if (s_scanning)
            return;
        s_scanning = true;
        try
        {
            var behaviours = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                var mb = behaviours[i];
                if (mb == null || mb.GetType().Name != "IngredientSplitter")
                    continue;
                if (!mb.gameObject.scene.IsValid())
                    continue; // 跳过 prefab 资产；资产由自定义内容读写时按需迁移
                TryMigrate(mb, true);
            }
        }
        catch (System.Exception ex)
        {
            LayoutEditorLog.LogWarning("[splitter-migrate] 扫描场景失败: " + ex.Message);
        }
        finally
        {
            s_scanning = false;
        }
    }

    [MenuItem(Menu)]
    private static void MigrateMenu()
    {
        s_cache.Clear();
        MigrateOpenScene();
        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        EditorUtility.DisplayDialog("分料机字段迁移", "已完成当前打开场景的 IngredientSplitter 输出字段迁移。\n旧 GameObject 引用已尽量解析为 PseudoPrefabSO。", "确定");
    }

    /// <summary>把单个分料机组件的旧 outputPrefab 迁移到 outputPrefabSO。
    ///  返回 true = 发生了迁移；无法解析时保留 legacy 并返回 false（不丢数据）。</summary>
    public static bool TryMigrate(Component splitter, bool log)
    {
        if (splitter == null)
            return false;
        var so = new SerializedObject(splitter);
        var target = so.FindProperty("outputPrefabSO");
        if (target == null)
            return false;
        if (target.objectReferenceValue != null)
            return false;

        var legacy = so.FindProperty("outputPrefabLegacy") ?? so.FindProperty("outputPrefab");
        if (legacy == null || !(legacy.objectReferenceValue is GameObject))
            return false;

        var legacyGo = (GameObject)legacy.objectReferenceValue;
        var resolved = Resolve(legacyGo);
        if (resolved == null)
        {
            if (log)
            {
                LayoutEditorLog.LogWarning("[splitter-migrate] 无法为旧输出 prefab 找到 PseudoPrefabSO: "
                    + legacyGo.name + "（" + AssetDatabase.GetAssetPath(legacyGo) + "）；"
                    + "该分料机输出暂保持空，请手动在自定义内容中重新选择。");
            }
            return false;
        }

        Undo.RecordObject(splitter, "Migrate IngredientSplitter Output SO");
        target.objectReferenceValue = resolved;
        legacy.objectReferenceValue = null;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(splitter);
        if (log)
        {
            LayoutEditorLog.Log("[splitter-migrate] " + splitter.gameObject.name + ": "
                + legacyGo.name + " -> " + AssetDatabase.GetAssetPath(resolved));
        }
        return true;
    }

    public static PseudoPrefabSO Resolve(GameObject legacy)
    {
        if (legacy == null)
            return null;
        int key = legacy.GetInstanceID();
        PseudoPrefabSO cached;
        if (s_cache.TryGetValue(key, out cached))
            return cached;

        // 1) prefab 自带 PseudoPrefabStub（基础/派生）
        var stub = legacy.GetComponent<PseudoPrefabStub>();
        if (stub != null && stub.pseudoPrefabSO != null)
            return Remember(key, stub.pseudoPrefabSO);

        var legacyPath = AssetDatabase.GetAssetPath(legacy);
        var legacyGuid = string.IsNullOrEmpty(legacyPath) ? "" : AssetDatabase.AssetPathToGUID(legacyPath);

        // 2) 全库 PseudoPrefabSO：asset 直引 / bundle:path 同源 / prefabName+文件名兜底
        var guids = AssetDatabase.FindAssets("t:PseudoPrefabSO");
        for (int i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var so = AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(path);
            if (so == null)
                continue;
            if (so.asset == legacy)
                return Remember(key, so);
            if (!string.IsNullOrEmpty(legacyPath))
            {
                var soAssetPath = so.asset != null ? AssetDatabase.GetAssetPath(so.asset) : "";
                if (!string.IsNullOrEmpty(soAssetPath) && soAssetPath == legacyPath)
                    return Remember(key, so);
            }
            if (!string.IsNullOrEmpty(legacyGuid) && so.assetPath != null
                && so.assetPath.IndexOf(legacyGuid, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return Remember(key, so);
            if (!string.IsNullOrEmpty(legacyPath)
                && string.Equals(System.IO.Path.GetFileNameWithoutExtension(legacyPath), so.prefabName,
                    System.StringComparison.OrdinalIgnoreCase))
                return Remember(key, so);
        }
        return null;
    }

    private static PseudoPrefabSO Remember(int key, PseudoPrefabSO so)
    {
        s_cache[key] = so;
        return so;
    }
}
