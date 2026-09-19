using System;
using System.Reflection;
using LevelEditor;
using LevelEditorStub;
using UnityEditor;
using UnityEngine;

/// <summary>
/// CustomStub 按需自动化（与 LayoutEditor 本体解耦的可选扩展，经钩子接入）：
///
/// 1. 域重载后（编译完成/脚本变更）自动补烘焙活动场景：数据载体在而组件缺失、
///    且统一运行时程序集现已可用 → RebakeRandomCratesInActiveScene 烘焙 + 保存场景
///    + 同步问号；并自动 StageRuntimeQuiet（Library DLL 比 .dll.bytes 新即重打包）；
/// 2. LayoutEditorDispenserIconFix.AfterRandomCrateSync（全部初始化路径汇聚点）→ 把
///    已烘焙随机箱的问号图标画到编辑器预览实例的箱盖上（复用运行时 PaintQuestionMark）。
///
/// 统一单程序集重构后：不再有母本→每集副本的拷贝/漂移同步（CustomStubCopyRequested
/// 钩子不再订阅）；运行时程序集 = 唯一母本 WebCustomStubRuntime。
/// </summary>
[InitializeOnLoad]
public static class CustomStubAutoBake
{
    static CustomStubAutoBake()
    {
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
            // RandomDispenser 包装 prefab 幂等兜底（正常已随仓库提供）
            CustomStubCopyTool.EnsureRandomDispenserPrefab();
            // RatHeist 包装 prefab + PseudoPrefabSO 幂等兜底
            CustomStubCopyTool.EnsureRatHeistPrefab();
            RebakeActiveScene();
            ArmIconSync();
            // 启动自动强制编译：母本源码比 DLL 新（外部改动未被 Unity 自动编译）时自动
            // 触发一次编译，编译域重载后回到本钩子自动打包——之后无需手动点击「编译」
            if (LayoutStubDllBuilder.RequestCompileIfStale())
                return; // 即将编译+域重载，staging 留给下一轮
            // 编译产物自动打包：Library DLL 比 .dll.bytes 新（源码更新后）即自动重新
            // staging，保证导出永远打包最新统一运行时 DLL
            if (LayoutStubDllBuilder.StageRuntimeQuiet())
                Debug.Log("[CustomStub] 已自动打包统一运行时 DLL（.dll.bytes → " + LayoutStubDllBuilder.RuntimeBundleName + "）");
        };
        // 聚焦监视：回到 Unity（编辑中改了源码、Unity 未自动编译的场景）即自动检查一次
        EditorApplication.update += FocusCompileTick;
    }

    // ---- 聚焦触发自动编译（一次性守卫：每次聚焦只检查一回，防重复 Refresh） ----
    private static bool _hadFocus;
    private static double _focusCheckAt = double.MaxValue;

    private static void FocusCompileTick()
    {
        var focused = EditorWindow.focusedWindow != null;
        if (focused && !_hadFocus)
            _focusCheckAt = EditorApplication.timeSinceStartup + 1.0; // 去抖：让聚焦自带刷新先跑
        _hadFocus = focused;
        if (!focused || EditorApplication.timeSinceStartup < _focusCheckAt)
            return;
        _focusCheckAt = double.MaxValue; // 本轮聚焦只检查一次，失焦后再聚焦重置
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlaying)
            return;
        LayoutStubDllBuilder.RequestCompileIfStale();
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

    private static void RebakeActiveScene()
    {
        try
        {
            var baked = LayoutEditorStubIO.RebakeRandomCratesInActiveScene();
            var portals = LayoutEditorStubIO.RebakeTeleportalExitOnlyInActiveScene();
            if (baked <= 0 && portals <= 0)
                return;
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
                return;
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            if (baked > 0)
                Debug.Log("[CustomStub] 已自动补烘焙 " + baked + " 个随机食材箱并保存场景: " + scene.path);
            if (portals > 0)
                Debug.Log("[CustomStub] 已自动补烘焙 " + portals + " 个单向传送门（仅作为出口）并保存场景: " + scene.path);
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
