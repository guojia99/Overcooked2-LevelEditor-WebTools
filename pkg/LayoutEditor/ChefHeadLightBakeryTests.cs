using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Editor-time checks for chef head light detection (reference OC1 4-2).</summary>
public static class ChefHeadLightBakeryTests
{
    private const string Story42 =
        "Assets/LevelSets/oc1_story/scenes/s_oc1_story_4_2.unity";
    private const string Template = "Assets/Template/s_template.unity";

    [MenuItem("Layout Editor/测试 (Tests)/运行厨师头灯烘焙器测试 (Run ChefHeadLightBakery Tests)", false, 601)]
    public static void RunAll()
    {
        var failed = 0;
        failed += AssertDetect(Story42, true, "OC1 4-2 exports head lights");
        failed += AssertDetect(Template, false, "template has no head lights");
        failed += AssertDarkEnv(Story42, true, "OC1 4-2 dark environment");
        failed += AssertDarkEnv(Template, false, "template default environment");

        if (failed == 0)
            Debug.Log("[LayoutEditor/Tests] 厨师头灯烘焙器测试：全部通过。");
        else
            Debug.LogError("[LayoutEditor/Tests] 厨师头灯烘焙器测试：" + failed + " 项失败。");
    }

    private static int AssertDetect(string scenePath, bool expected, string label)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        var actual = ChefHeadLightBakery.DetectEnabledInScene(scene);
        if (actual == expected)
            return 0;
        Debug.LogError("[LayoutEditor/Tests] " + label + "：期望 " + expected + "，实际 " + actual);
        return 1;
    }

    private static int AssertDarkEnv(string scenePath, bool expectedDark, string label)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        var actual = ChefDarkEnvironmentBakery.DetectDarkEnvironmentInScene(scene);
        if (actual == expectedDark)
            return 0;
        var day = ChefDarkEnvironmentBakery.ReadDayLightIntensity(scene);
        Debug.LogError("[LayoutEditor/Tests] " + label + "：期望暗关=" + expectedDark + "，实际=" + actual
            + "，day intensity=" + day);
        return 1;
    }
}
