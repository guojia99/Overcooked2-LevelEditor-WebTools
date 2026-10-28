using System;
using System.IO;
using System.Text;
using LevelEditorStub;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 关卡布局的一次性数据迁移。
///
/// 2026-10-28：茶杯回收台（web_teacup_return）早期默认 returnClean=true（连
/// 旧版写回也把回收台当普通盘子回收台），导致旧关卡上菜后返回干净杯/不返回。
/// 产品口径改为“默认脏杯、可手动勾选干净杯”，因此旧关卡需要一次性把
/// returnClean 从 true 改成 false；用户之后手动勾选 true 必须保留。
///
/// 持久化标记放在关卡 data 目录的 `layout_migrations~`（~ 目录不进包）：
///   Assets/LevelSets/&lt;set&gt;/data/&lt;level&gt;/layout_migrations~/layout_migrations.json
/// 写回/导出 prepare 时若标记未落盘则迁移一次，成功后 CommitPending 再写标记。
/// </summary>
public static class LayoutEditorLayoutMigrations
{
    private const int SchemaVersion = 1;
    private const string StoreFileName = "layout_migrations.json";

    private static string s_pendingDir;
    private static MigrationState s_pendingState;
    private static bool s_pendingActive;

    [Serializable]
    public class MigrationState
    {
        public int schemaVersion;
        public bool teacupReturnCleanDefaultV2;
    }

    /// <summary>每次 Apply 开始清掉上次未提交的迁移（写回失败/提前返回时不会误落标记）。</summary>
    public static void BeginApply()
    {
        s_pendingDir = null;
        s_pendingState = null;
        s_pendingActive = false;
    }

    /// <summary>新建关卡时立即写迁移基线：新关卡默认 returnClean=false，但用户
    /// 手动勾选干净杯必须保留，不能被“旧关卡迁移”误伤。</summary>
    public static void MarkLevelCreated(string sceneAssetPath)
    {
        var dir = ResolveMigrationDir(sceneAssetPath);
        if (string.IsNullOrEmpty(dir))
            return;
        var state = Load(dir) ?? new MigrationState();
        state.schemaVersion = SchemaVersion;
        state.teacupReturnCleanDefaultV2 = true;
        Save(dir, state);
        LayoutEditorLog.Log("[LayoutEditor] 新关卡迁移基线: " + sceneAssetPath);
    }

    /// <summary>写回前迁移文档：只改本次写回前已存在的旧实例（instanceId 不是 new:），
    /// 本次新建实例保留用户显式勾选。返回迁移数量。</summary>
    public static int ApplyBeforeWriteBack(LayoutDocumentDto document)
    {
        if (document == null || document.items == null)
            return 0;
        var scenePath = !string.IsNullOrEmpty(document.sceneAssetPath)
            ? document.sceneAssetPath
            : UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path;
        var dir = ResolveMigrationDir(scenePath);
        if (string.IsNullOrEmpty(dir))
            return 0;
        var state = Load(dir);
        if (state != null && state.teacupReturnCleanDefaultV2)
            return 0;

        int migrated = 0;
        for (int i = 0; i < document.items.Length; i++)
        {
            var item = document.items[i];
            if (item == null || item.plateReturn == null || !item.plateReturn.returnClean)
                continue;
            if (!IsTeacupReturnItem(item))
                continue;
            // 本次新建的回收台保留用户显式勾选；只迁移旧场景里的存量实例。
            if (!string.IsNullOrEmpty(item.instanceId)
                && item.instanceId.StartsWith("new:", StringComparison.Ordinal))
                continue;
            item.plateReturn.returnClean = false;
            migrated++;
        }

        state = state ?? new MigrationState();
        state.schemaVersion = SchemaVersion;
        if (migrated > 0)
        {
            // 场景还没保存成功，先挂起；CommitPending 在 SaveScene 之后写标记。
            s_pendingDir = dir;
            s_pendingState = state;
            s_pendingActive = true;
            LayoutEditorLog.Log("[LayoutEditor] 旧关卡茶杯回收台迁移: returnClean true -> false ×"
                + migrated + "（" + scenePath + "）");
        }
        else
        {
            // 没有需要迁移的存量实例：可以安全落标记，后续新放置的回收台
            // 手动勾选干净杯不会被再次迁移。
            state.teacupReturnCleanDefaultV2 = true;
            Save(dir, state);
        }
        return migrated;
    }

    /// <summary>场景打开/导出 prepare 兜底：关卡无迁移基线（旧关卡）时，把场景里
    /// 所有茶杯回收台 returnClean=true 一次性迁移为 false。新关卡在 CreateLevel
    /// 时已写基线，手动勾选的干净杯不受影响。
    /// 返回迁移数量；调用方需在 SaveScene 后 CommitPending。</summary>
    public static int ApplyToOpenScene(Scene scene)
    {
        BeginApply();
        var dir = ResolveMigrationDir(scene.path);
        if (string.IsNullOrEmpty(dir))
            return 0;
        var state = Load(dir);
        if (state != null && state.teacupReturnCleanDefaultV2)
            return 0;

        int migrated = 0;
        bool sawTeacupWithoutDerivedStub = false;
        var seenObjects = new System.Collections.Generic.HashSet<GameObject>();
        var allStubs = UnityEngine.Object.FindObjectsOfType<PseudoPrefabStub>();
        for (int i = 0; i < allStubs.Length; i++)
        {
            var anyStub = allStubs[i];
            if (anyStub == null)
                continue;
            var anyGo = anyStub.gameObject;
            if (!seenObjects.Add(anyGo))
                continue;
            if (!IsTeacupReturnObject(anyGo))
                continue;
            // 同一物体上 base+derived 共存是待提升状态，不算“缺派生 stub”；
            // 只有完全没有 PseudoPrefabPlateReturnStub 的茶杯回收台才延后落标记。
            if (anyGo.GetComponent<PseudoPrefabPlateReturnStub>() == null)
            {
                sawTeacupWithoutDerivedStub = true;
                break;
            }
        }
        var stubs = UnityEngine.Object.FindObjectsOfType<PseudoPrefabPlateReturnStub>();
        for (int i = 0; i < stubs.Length; i++)
        {
            var stub = stubs[i];
            if (stub == null || !stub.returnClean)
                continue;
            if (!IsTeacupReturnObject(stub.gameObject))
                continue;
            Undo.RecordObject(stub, "Layout Editor Teacup Return Migration");
            stub.returnClean = false;
            EditorUtility.SetDirty(stub);
            migrated++;
        }

        if (migrated > 0)
        {
            s_pendingDir = dir;
            s_pendingState = state ?? new MigrationState { schemaVersion = SchemaVersion };
            s_pendingActive = true;
            LayoutEditorLog.Log("[LayoutEditor] 迁移旧关卡茶杯回收台: returnClean true -> false ×"
                + migrated + "（" + scene.path + "）");
        }
        else if (!sawTeacupWithoutDerivedStub)
        {
            // 有 base-only 茶杯回收台时先不落标记：等写回补派生 stub 后再迁移
            // （否则旧关卡 true 会被永久保留）。
            state = state ?? new MigrationState();
            state.schemaVersion = SchemaVersion;
            state.teacupReturnCleanDefaultV2 = true;
            Save(dir, state);
        }
        return migrated;
    }

    /// <summary>场景保存成功后落盘迁移标记。</summary>
    public static void CommitPending()
    {
        if (!s_pendingActive || string.IsNullOrEmpty(s_pendingDir))
        {
            BeginApply();
            return;
        }
        s_pendingState = s_pendingState ?? new MigrationState();
        s_pendingState.schemaVersion = SchemaVersion;
        s_pendingState.teacupReturnCleanDefaultV2 = true;
        Save(s_pendingDir, s_pendingState);
        s_pendingDir = null;
        s_pendingState = null;
        s_pendingActive = false;
    }

    /// <summary>写回/导出失败时丢弃待提交标记（下次重新迁移）。</summary>
    public static void DiscardPending()
    {
        s_pendingDir = null;
        s_pendingState = null;
        s_pendingActive = false;
    }

    private static bool IsTeacupReturnItem(LayoutItemDto item)
    {
        var path = item.prefabAssetPath;
        if (string.IsNullOrEmpty(path) && !string.IsNullOrEmpty(item.prefabGuid))
            path = AssetDatabase.GUIDToAssetPath(item.prefabGuid);
        return !string.IsNullOrEmpty(path)
            && path.IndexOf("web_teacup_return", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsTeacupReturnObject(GameObject go)
    {
        if (go == null)
            return false;
        if (!string.IsNullOrEmpty(go.name)
            && go.name.IndexOf("web_teacup_return", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        var prefab = PrefabUtility.GetPrefabParent(go) as GameObject;
        if (prefab != null && !string.IsNullOrEmpty(prefab.name)
            && prefab.name.IndexOf("web_teacup_return", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        var stubs = go.GetComponents<PseudoPrefabStub>();
        for (int i = 0; i < stubs.Length; i++)
        {
            var stub = stubs[i];
            if (stub != null && stub.pseudoPrefabSO != null && !string.IsNullOrEmpty(stub.pseudoPrefabSO.name)
                && stub.pseudoPrefabSO.name.IndexOf("TeacupReturn", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
    }

    private static string ResolveMigrationDir(string sceneAssetPath)
    {
        if (string.IsNullOrEmpty(sceneAssetPath))
            return null;
        var parts = sceneAssetPath.Replace('\\', '/').Split('/');
        if (parts.Length < 5 || parts[0] != "Assets" || parts[1] != "LevelSets")
            return null;
        var setName = parts[2];
        var levelName = Path.GetFileNameWithoutExtension(parts[parts.Length - 1]);
        if (string.IsNullOrEmpty(setName) || string.IsNullOrEmpty(levelName))
            return null;
        var dataRoot = "Assets/LevelSets/" + setName + "/data/";
        // 旧场景名 s_<id> 的 data 目录是无前缀 levelId（同 PartLevelStore 约定）；
        // 新约定下若 s_ 是真实 levelId，则 data/s_<id> 已存在，按原名走。
        if (!AssetDatabase.IsValidFolder(dataRoot + levelName)
            && levelName.StartsWith("s_", StringComparison.Ordinal) && levelName.Length > 2)
            levelName = levelName.Substring(2);
        return dataRoot + levelName + "/layout_migrations~";
    }

    private static MigrationState Load(string dir)
    {
        try
        {
            var file = Path.Combine(dir, StoreFileName);
            if (!File.Exists(file))
                return null;
            var text = File.ReadAllText(file);
            return string.IsNullOrEmpty(text) ? null : JsonUtility.FromJson<MigrationState>(text);
        }
        catch (Exception ex)
        {
            LayoutEditorLog.LogWarning("[LayoutEditor] 读取布局迁移标记失败（按未迁移处理）: "
                + dir + " — " + ex.Message);
            return null;
        }
    }

    private static void Save(string dir, MigrationState state)
    {
        try
        {
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, StoreFileName),
                JsonUtility.ToJson(state, true), new UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            LayoutEditorLog.LogWarning("[LayoutEditor] 写入布局迁移标记失败（下次会重试）: "
                + dir + " — " + ex.Message);
        }
    }
}
