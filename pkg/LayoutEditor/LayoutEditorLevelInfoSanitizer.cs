using LevelEditorStub;
using UnityEditor;
using UnityEngine;

/// <summary>宿主原版 PseudoPrefabManager.SetAssetRef 会直接遍历
/// levelInfo.inLevelAmbiences（.Select）和 levelInfo.audioDirectorySOs（.Length），
/// 旧关卡的 LevelInfoSO 缺少这些字段时反序列化为 null，场景一打开就抛
/// ArgumentNullException；audioDirectorySOs 为空的关卡在运行时还会让宿主
/// AudioManager.FindEntry 越界。宿主文件不可改动，因此插件侧在域重载后
/// 对项目内所有 LevelInfoSO 做一次迁移：null 数组补成空数组，完全没有音频
/// 配置的关卡用 levelinfo 模板默认值回填。兼容 Unity 2017（无 sceneOpening 事件）。</summary>
[InitializeOnLoad]
public static class LayoutEditorLevelInfoSanitizer
{
    private const string TemplateLevelInfoPath = "Assets/Template/levelinfo_template.asset";
    private static double _lastMigrateTime = -10.0;
    private static LevelInfoSO _template;
    private static bool _templateLoaded;

    static LayoutEditorLevelInfoSanitizer()
    {
        // 静态构造函数里先迁移一次：尽量赶在编辑器启动恢复场景（触发宿主
        // OnEnable → Init）之前把旧资产修好。AssetDatabase 未就绪时忽略，
        // 由 delayCall / hierarchyWindowChanged 兜底。
        try
        {
            MigrateAll();
        }
        catch
        {
        }
        EditorApplication.delayCall += MigrateAll;
        EditorApplication.hierarchyWindowChanged += OnHierarchyChanged;
    }

    private static void OnHierarchyChanged()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        if (EditorApplication.timeSinceStartup - _lastMigrateTime < 2.0)
            return;
        try
        {
            MigrateAll();
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[LayoutEditor] LevelInfoSO sanitize failed: " + ex.Message);
        }
    }

    /// <summary>迁移项目内所有 LevelInfoSO；只在确有改动时才保存资产。</summary>
    public static void MigrateAll()
    {
        _lastMigrateTime = EditorApplication.timeSinceStartup;
        foreach (var guid in AssetDatabase.FindAssets("t:LevelInfoSO"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var info = AssetDatabase.LoadAssetAtPath<LevelInfoSO>(path);
            Sanitize(info);
        }
    }

    /// <summary>null 数组补成空数组；audioDirectorySOs 为空的旧关卡用模板默认值回填
    /// （仅回填未设置的字段）。有改动时保存资产。返回是否有改动。</summary>
    public static bool Sanitize(LevelInfoSO info)
    {
        if (info == null)
            return false;

        var changed = false;
        if (info.inLevelAmbiences == null)
        {
            info.inLevelAmbiences = new LevelInfoSO.GameLoopingAudioTag[0];
            changed = true;
        }
        if (info.audioDirectorySOs == null)
        {
            info.audioDirectorySOs = new PseudoPrefabSO[0];
            changed = true;
        }
        if (info.dependencies == null)
        {
            info.dependencies = new string[0];
            changed = true;
        }

        // 旧关卡（场景重存后旧 stub 字段丢失）没有任何音频配置：运行时宿主
        // AudioManager.FindEntry 会越界，用模板默认值回填未设置的字段。
        if (info.audioDirectorySOs.Length == 0)
        {
            var tpl = LoadTemplate();
            if (tpl != null && tpl.audioDirectorySOs != null && tpl.audioDirectorySOs.Length > 0
                && tpl != info)
            {
                info.audioDirectorySOs = (PseudoPrefabSO[])tpl.audioDirectorySOs.Clone();
                if (info.inLevelMusicSO == null)
                    info.inLevelMusicSO = tpl.inLevelMusicSO;
                if (info.inLevelAmbiences.Length == 0 && tpl.inLevelAmbiences != null)
                    info.inLevelAmbiences =
                        (LevelInfoSO.GameLoopingAudioTag[])tpl.inLevelAmbiences.Clone();
                if (info.onDeathEffectSO == null)
                    info.onDeathEffectSO = tpl.onDeathEffectSO;
                changed = true;
            }
        }

        // inLevelAmbiences 里的死枚举值（WashingUp/Sizzling 等 6 个无任何
        // AudioDirectoryData 条目）会让运行时 AudioManager.FindEntry 对空列表
        // 取下标越界，同样属于必须迁移的坏数据。
        var removedAmb = LayoutEditorLevelAdminApi.StripInvalidAmbiences(info);
        if (removedAmb != null)
        {
            Debug.LogWarning("[LayoutEditor] removed invalid ambiences (no audio resource) from "
                + AssetDatabase.GetAssetPath(info) + ": " + string.Join(", ", removedAmb.ToArray()));
            changed = true;
        }

        if (!changed)
            return false;

        EditorUtility.SetDirty(info);
        AssetDatabase.SaveAssets();
        Debug.Log("[LayoutEditor] Migrated LevelInfoSO audio fields: " + AssetDatabase.GetAssetPath(info));
        return true;
    }

    private static LevelInfoSO LoadTemplate()
    {
        if (!_templateLoaded)
        {
            _templateLoaded = true;
            _template = AssetDatabase.LoadAssetAtPath<LevelInfoSO>(TemplateLevelInfoPath);
        }
        return _template;
    }
}
