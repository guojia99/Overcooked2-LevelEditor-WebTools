using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// 暗关环境（对齐 s_oc1_story_4_2）：与 chefHeadLightsEnabled 联动写回。
/// 环境光近黑 + Art/Lights/day 强度 0；关闭头灯时恢复 s_template 默认值。
/// </summary>
public static class ChefDarkEnvironmentBakery
{
    public const string DayLightPath = "Art/Lights/day";
    public const float TemplateDayIntensity = 0.7f;
    public const float DarkDayIntensity = 0f;

    private const string DarkSkyboxPath = "Assets/common01/materials/mat_sunny3_skybox.mat";

    // s_oc1_story_4_2 RenderSettings
    private static readonly Color DarkSky = Color.black;
    private static readonly Color DarkEquator = new Color(0.20392157f, 0.28627452f, 0.26666668f, 1f);
    private static readonly Color DarkGround = new Color(0.36078432f, 0.48235294f, 0.2784314f, 1f);
    private const AmbientMode DarkAmbientMode = (AmbientMode)3;

    // s_template RenderSettings
    private static readonly Color TemplateSky = new Color(0.212f, 0.227f, 0.259f, 1f);
    private static readonly Color TemplateEquator = new Color(0.114f, 0.125f, 0.133f, 1f);
    private static readonly Color TemplateGround = new Color(0.047f, 0.043f, 0.035f, 1f);

    public static void ApplyDark()
    {
        ApplyRenderSettingsDark();
        SetDayLightIntensity(DarkDayIntensity);
        MarkActiveSceneDirty();
    }

    public static void ApplyTemplateDefault()
    {
        ApplyRenderSettingsTemplate();
        SetDayLightIntensity(TemplateDayIntensity);
        MarkActiveSceneDirty();
    }

    /// <summary>4-2 暗关：环境光黑 + day≈0。</summary>
    public static bool DetectDarkEnvironmentInScene(Scene scene)
    {
        if (!IsAmbientNearDark())
            return false;
        return Mathf.Approximately(ReadDayLightIntensity(scene), DarkDayIntensity);
    }

    /// <summary>
    /// 厨师头灯关闭时是否仍需把场景从暗关快照恢复为模板。
    /// 未开过暗关的 DIY 场景不应每次写回都覆盖 RenderSettings。
    /// </summary>
    public static bool NeedsTemplateEnvironmentRestore()
    {
        if (IsAmbientNearDark())
            return true;
        if (IsUsingDarkSkybox())
            return true;
        return Mathf.Approximately(ReadDayLightIntensity(SceneManager.GetActiveScene()), DarkDayIntensity);
    }

    public static float ReadDayLightIntensity(Scene scene)
    {
        var t = FindDayLightTransform(scene);
        if (t == null)
            return TemplateDayIntensity;
        var light = t.GetComponent<Light>();
        if (light == null)
            return TemplateDayIntensity;
        return light.intensity;
    }

    private static void ApplyRenderSettingsDark()
    {
        RenderSettings.ambientMode = DarkAmbientMode;
        RenderSettings.ambientSkyColor = DarkSky;
        RenderSettings.ambientEquatorColor = DarkEquator;
        RenderSettings.ambientGroundColor = DarkGround;
        RenderSettings.ambientIntensity = 1f;

        var sky = AssetDatabase.LoadAssetAtPath<Material>(DarkSkyboxPath);
        if (sky != null)
            RenderSettings.skybox = sky;

        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
        RenderSettings.reflectionIntensity = 1f;
    }

    private static void ApplyRenderSettingsTemplate()
    {
        RenderSettings.ambientMode = AmbientMode.Skybox;
        RenderSettings.ambientSkyColor = TemplateSky;
        RenderSettings.ambientEquatorColor = TemplateEquator;
        RenderSettings.ambientGroundColor = TemplateGround;
        RenderSettings.ambientIntensity = 1f;

        var defaultSky = GetTemplateDefaultSkybox();
        RenderSettings.skybox = defaultSky;

        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
        RenderSettings.reflectionIntensity = 1f;
        RenderSettings.customReflection = null;
    }

    private static Material GetTemplateDefaultSkybox()
    {
        // 与 s_template m_SkyboxMaterial fileID 10304（内置 Default-Skybox）一致；null 在部分版本不会清掉暗关天空盒。
        return AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
    }

    private static void SetDayLightIntensity(float intensity)
    {
        var t = LayoutEditorHierarchy.FindByPath(DayLightPath);
        if (t == null)
            return;
        var light = t.GetComponent<Light>();
        if (light == null)
            return;
        Undo.RecordObject(light, "Layout Editor Chef Dark Environment");
        light.intensity = Mathf.Max(0f, intensity);
        EditorUtility.SetDirty(light);
    }

    private static Transform FindDayLightTransform(Scene scene)
    {
        if (!scene.IsValid())
            return null;
        return LayoutEditorHierarchy.FindByPath(DayLightPath);
    }

    private static bool IsAmbientNearDark()
    {
        var c = RenderSettings.ambientSkyColor;
        return c.r < 0.02f && c.g < 0.02f && c.b < 0.02f;
    }

    private static bool IsUsingDarkSkybox()
    {
        var mat = RenderSettings.skybox;
        if (mat == null)
            return false;
        return AssetDatabase.GetAssetPath(mat) == DarkSkyboxPath;
    }

    private static void MarkActiveSceneDirty()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.IsValid())
            EditorSceneManager.MarkSceneDirty(scene);
    }
}
