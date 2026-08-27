using System;
using UnityEditor;
using UnityEngine;

/// <summary>Editor-time checks for MoveControlBakery asset key generation.</summary>
public static class MoveControlBakeryTests
{
    private const string SceneName = "s_jia_level1_3";

    [MenuItem("Layout Editor/Tests/Run MoveControlBakery Tests")]
    public static void RunAll()
    {
        var failed = 0;
        failed += AssertNotEqual(
            "scene-imported Chinese groups get distinct keys",
            KeyFor("莲花灯", "scene:Design/Animated Objects/莲花灯",
                "Design/Animated Objects/莲花灯"),
            KeyFor("孔明灯", "scene:Design/Animated Objects/孔明灯",
                "Design/Animated Objects/孔明灯"));
        failed += AssertEqual(
            "UUID id keeps stable 8-char suffix",
            KeyFor("莲花灯", "7d153a50-fb4a-4a62-9f81-7dd59a66de02", null),
            SceneName + "_group_7d153a50");
        failed += AssertNotEqual(
            "ASCII display name differs from Chinese scene group",
            KeyFor("移动NPC1", "scene:Design/Animated Objects/移动NPC1",
                "Design/Animated Objects/移动NPC1"),
            KeyFor("莲花灯", "scene:Design/Animated Objects/莲花灯",
                "Design/Animated Objects/莲花灯"));
        failed += AssertNotEqual(
            "孔明灯2 suffix differs from 孔明灯",
            KeyFor("孔明灯2", "scene:Design/Animated Objects/孔明灯2",
                "Design/Animated Objects/孔明灯2"),
            KeyFor("孔明灯", "scene:Design/Animated Objects/孔明灯",
                "Design/Animated Objects/孔明灯"));
        failed += AssertEqual(
            "same group id is stable across calls",
            KeyFor("莲花灯", "scene:Design/Animated Objects/莲花灯",
                "Design/Animated Objects/莲花灯"),
            KeyFor("莲花灯", "scene:Design/Animated Objects/莲花灯",
                "Design/Animated Objects/莲花灯"));
        failed += AssertNotEqual(
            "legacy collision keys are no longer produced for 莲花灯/孔明灯",
            KeyFor("莲花灯", "scene:Design/Animated Objects/莲花灯",
                "Design/Animated Objects/莲花灯"),
            SceneName + "_group_scene_De");

        if (failed == 0)
            Debug.Log("[LayoutEditor] MoveControlBakery tests: all passed.");
        else
            Debug.LogError("[LayoutEditor] MoveControlBakery tests: " + failed + " failed.");
    }

    private static string KeyFor(string displayName, string id, string hierarchyPath)
    {
        return MoveControlBakery.BuildAssetKey(SceneName, new MoveGroupDto
        {
            displayName = displayName,
            id = id,
            groupHierarchyPath = hierarchyPath
        });
    }

    private static int AssertEqual(string label, string actual, string expected)
    {
        if (string.Equals(actual, expected, StringComparison.Ordinal))
            return 0;
        Debug.LogError("[LayoutEditor] FAIL " + label + ": expected \"" + expected +
            "\", got \"" + actual + "\"");
        return 1;
    }

    private static int AssertNotEqual(string label, string a, string b)
    {
        if (!string.Equals(a, b, StringComparison.Ordinal))
            return 0;
        Debug.LogError("[LayoutEditor] FAIL " + label + ": both keys are \"" + a + "\"");
        return 1;
    }
}
