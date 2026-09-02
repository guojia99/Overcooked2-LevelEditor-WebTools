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
        EditorApplication.delayCall += delegate
        {
            // 母本内容漂移自动同步（改 RandomCrate.cs 后各关卡集副本自动跟进）
            CustomStubCopyTool.SyncAllDrifted();
            // RandomDispenser 包装 prefab 幂等兜底（正常已随仓库提供）
            CustomStubCopyTool.EnsureRandomDispenserPrefab();
            RebakeActiveScene();
        };
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
    /// 自身在同步完成后绘制；本方法只覆盖编辑器预览（场景视图/写回后立即可见）。</summary>
    private static void SyncQuestionMarks()
    {
        try
        {
            var painted = 0;
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
                    skipped++;
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
                Debug.Log("[CustomStub] 问号图标同步: 画 " + painted + " 个，跳过 " + skipped + " 个（组件/贴图/预览实例缺失）");
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
