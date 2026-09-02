using System;
using System.Reflection;
using LevelEditor;
using LevelEditorStub;
using UnityEditor;
using UnityEngine;

/// <summary>
/// CustomStub 按需自动化（与 LayoutEditor 本体解耦的可选扩展，经钩子接入）：
///
/// 1. LayoutEditorStubIO.CustomStubCopyRequested（写回随机食材箱但关卡集尚无
///    stub 程序集时发起）→ 自动把母本拷入 Assets/LevelSets/&lt;set&gt;/stub/
///    （CustomStubCopyTool.CopyToSet，内容同步、GUID 稳定）→ Refresh 触发编译；
/// 2. 域重载后（编译完成/脚本变更）自动补烘焙活动场景：数据载体在而组件缺失、
///    且程序集现已可用 → RebakeRandomCratesInActiveScene 烘焙 + 保存场景 + 同步问号；
/// 3. LayoutEditorDispenserIconFix.AfterRandomCrateSync（SafeReinit /
///    ReloadPseudoAssets / 自愈守卫等全部初始化路径汇聚点）→ 把已烘焙随机箱的
///    问号图标画到编辑器预览实例的箱盖上（复用运行时 PaintQuestionMark 单一实现）。
///    由此形成「写回 → 自动拷贝 → 编译 → 自动烘焙 → 问号可见」的零手动闭环。
/// </summary>
[InitializeOnLoad]
public static class CustomStubAutoBake
{
    static CustomStubAutoBake()
    {
        LayoutEditorStubIO.CustomStubCopyRequested += OnCopyRequested;
        LayoutEditorDispenserIconFix.AfterRandomCrateSync += SyncQuestionMarks;
        // 宿主 PseudoPrefabManager.OnEnable 在编辑模式（场景打开/域重载）就会 Init——
        // 实例化子物体并画首食材图标，该路径不经过 SyncSeededIcons 钩子。这里兜底：
        // 场景打开/退出 Play/域重载后短程轮询，子物体就绪即补画问号。
        UnityEditor.SceneManagement.EditorSceneManager.sceneOpened += delegate (UnityEngine.SceneManagement.Scene scene, UnityEditor.SceneManagement.OpenSceneMode mode)
        {
            ArmIconSync();
        };
        EditorApplication.playModeStateChanged += delegate (PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                ArmIconSync();
        };
        EditorApplication.delayCall += delegate
        {
            // 母本内容漂移自动同步（改 RandomCrate.cs 后各关卡集副本自动跟进）
            CustomStubCopyTool.SyncAllDrifted();
            // RandomDispenser 包装 prefab 幂等兜底（正常已随仓库提供）
            CustomStubCopyTool.EnsureRandomDispenserPrefab();
            RebakeActiveScene();
            // 编译产物自动打包：Library DLL 比 .dll.bytes 新（首次拷贝编译后/源码更新后）
            // 即自动重新 staging，保证导出永远打包最新 DLL
            var staged = LayoutStubDllBuilder.StageAllSetsQuiet();
            if (staged > 0)
                Debug.Log("[CustomStub] 已自动打包 " + staged + " 个关卡集的 Stub DLL（.dll.bytes → <set>/runtime）");
            ArmIconSync();
        };
    }

    // ---- 问号补画轮询（同 LayoutEditorDispenserIconHeal 的短程守卫模式） ----
    private static bool _iconArmed;
    private static double _iconDeadline;

    private static void ArmIconSync()
    {
        _iconArmed = true;
        _iconDeadline = EditorApplication.timeSinceStartup + 15.0;
        EditorApplication.update -= SyncIconTick;
        EditorApplication.update += SyncIconTick;
    }

    private static void SyncIconTick()
    {
        if (!_iconArmed)
        {
            EditorApplication.update -= SyncIconTick;
            return;
        }
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;
        if (EditorApplication.timeSinceStartup > _iconDeadline)
        {
            _iconArmed = false;
            EditorApplication.update -= SyncIconTick;
            return;
        }
        try
        {
            int painted;
            int waiting;
            SyncQuestionMarksCore(out painted, out waiting);
            if (painted > 0)
            {
                UnityEditor.SceneView.RepaintAll();
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
            }
            if (waiting <= 0)
            {
                _iconArmed = false;
                EditorApplication.update -= SyncIconTick;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[CustomStub] 问号补画轮询异常: " + ex.Message);
            _iconArmed = false;
            EditorApplication.update -= SyncIconTick;
        }
    }

    private static void OnCopyRequested(string setName)
    {
        // 延迟到当前写回/保存流程结束后再拷贝+Refresh，避免打断主线程操作
        EditorApplication.delayCall += delegate
        {
            try
            {
                var result = CustomStubCopyTool.CopyToSet(setName);
                Debug.Log("[CustomStub] 自动拷贝 " + setName + ": " + result
                    + "（编译完成后将自动烘焙随机食材箱）");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        };
    }

    private static void RebakeActiveScene()
    {
        try
        {
            var baked = LayoutEditorStubIO.RebakeRandomCratesInActiveScene();
            if (baked <= 0)
                return;
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
                return;
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            Debug.Log("[CustomStub] 已自动补烘焙 " + baked + " 个随机食材箱并保存场景: " + scene.path);
            SyncQuestionMarks();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[CustomStub] 自动补烘焙失败: " + ex.Message);
        }
    }

    /// <summary>编辑器侧问号同步：场景里已烘焙的 RandomCrate（经数据载体定位），
    /// 把问号画到其 PseudoPrefab 预览实例的箱盖上。运行时（Play/游戏）由组件
    /// 自身在同步完成后绘制；本方法只覆盖编辑器预览（场景视图/写回后立即可见）。
    /// 画完立即强制重绘 Scene 视图（反射改材质不会自动触发重绘）；
    /// 有子物体未就绪的箱子转入短程轮询追画。</summary>
    private static void SyncQuestionMarks()
    {
        int painted;
        int waiting;
        SyncQuestionMarksCore(out painted, out waiting);
        if (painted > 0)
        {
            UnityEditor.SceneView.RepaintAll();
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
        }
        if (waiting > 0)
            ArmIconSync();
    }

    /// <summary>核心补画：返回 painted=已画数量；waiting=组件/贴图就绪但子物体尚未
    /// 实例化（宿主 Init 未跑到）的数量——调用方据此决定是否继续轮询。</summary>
    private static void SyncQuestionMarksCore(out int painted, out int waiting)
    {
        painted = 0;
        waiting = 0;
        try
        {
            var skipped = 0;
            var tags = UnityEngine.Object.FindObjectsOfType<SpecificPseudoPrefabTag>();
            if (tags == null)
                return;
            foreach (var tag in tags)
            {
                if (tag == null || string.IsNullOrEmpty(tag.prefabTag) ||
                    !tag.prefabTag.StartsWith("RandomCrate|", StringComparison.Ordinal))
                    continue;
                var go = tag.gameObject;
                var type = LayoutEditorStubIO.FindRandomCrateType(go);
                if (type == null)
                    continue;
                var comp = go.GetComponent(type);
                if (comp == null)
                {
                    skipped++;
                    continue;
                }
                var texture = GetRandomCrateField(comp, "m_questionMarkTexture") as Texture2D;
                if (texture == null)
                {
                    skipped++;
                    continue;
                }
                var pseudo = go.GetComponent<PseudoPrefab>();
                var child = pseudo != null ? pseudo.childGameObject : null;
                if (child == null)
                {
                    waiting++;
                    continue;
                }
                var paint = type.GetMethod("PaintQuestionMark",
                    BindingFlags.Public | BindingFlags.Static);
                if (paint == null)
                    continue;
                paint.Invoke(null, new object[] { child, texture });
                painted++;
            }
            if (painted > 0 || skipped > 0)
                Debug.Log("[CustomStub] 问号图标同步: 画 " + painted + " 个，等待子物体 " + waiting
                    + " 个，跳过 " + skipped + " 个（组件/贴图缺失）");
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[CustomStub] 问号图标同步失败: " + ex.Message);
        }
    }

    private static object GetRandomCrateField(Component comp, string fieldName)
    {
        if (comp == null)
            return null;
        var f = comp.GetType().GetField(fieldName);
        return f != null ? f.GetValue(comp) : null;
    }
}
