using System;
using UnityEditor;
using UnityEngine;

/// <summary>Editor-time checks for AnimGroupBakery asset key generation.</summary>
public static class AnimGroupBakeryTests
{
    private const string SceneName = "s_jia_level1_3";

    [MenuItem("Layout Editor/Tests/Run AnimGroupBakery Tests")]
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

        failed += TimelineMigrationTests.Run();
        failed += FxEventTests.Run();

        if (failed == 0)
            Debug.Log("[LayoutEditor] AnimGroupBakery tests: all passed.");
        else
            Debug.LogError("[LayoutEditor] AnimGroupBakery tests: " + failed + " failed.");
    }

    /// <summary>时间轴模型：旧顺序事件迁移、事件时长与时间簇装箱。</summary>
    private static class TimelineMigrationTests
    {
        public static int Run()
        {
            int failed = 0;

            // 迁移：delay 链 → 绝对 startTime（waitForFinished=false 规则）。
            var g = new AnimGroupDto
            {
                id = "t1",
                displayName = "T1",
                waitForFinished = false,
                events = new[]
                {
                    new AnimGroupEventDto { id = "a", type = "wait", delay = 2f, duration = 3f },
                    new AnimGroupEventDto { id = "b", type = "wait", delay = 1f, duration = 1f },
                    new AnimGroupEventDto { id = "c", type = "wait", delay = 0f, duration = 0.5f }
                },
                waypoints = new AnimGroupWaypointDto[0]
            };
            AnimGroupBakery.MigrateEventTimeline(g, null);
            failed += AssertFloat("first event start = its delay", g.events[0].startTime, 2f);
            failed += AssertFloat("second start = prev start + max(prevDur, delay)", g.events[1].startTime, 5f);
            failed += AssertFloat("delay 0 chains at previous clip end", g.events[2].startTime, 6f);
            // 幂等：已有 startTime 的组不再迁移。
            g.events[0].startTime = 9f;
            AnimGroupBakery.MigrateEventTimeline(g, null);
            failed += AssertFloat("migration is idempotent", g.events[0].startTime, 9f);

            // 时间簇：重叠区间并入同簇，相接/间隔开新簇。
            var g2 = new AnimGroupDto
            {
                id = "t2",
                displayName = "T2",
                events = new[]
                {
                    new AnimGroupEventDto { id = "m", type = "wait", duration = 4f, startTime = 0f },
                    new AnimGroupEventDto { id = "r", type = "rotate", rotateSeconds = 2f, startTime = 1f },
                    new AnimGroupEventDto { id = "w", type = "wait", duration = 1f, startTime = 4f }
                },
                waypoints = new AnimGroupWaypointDto[0]
            };
            var sorted = new System.Collections.Generic.List<AnimGroupEventDto>(g2.events);
            sorted.Sort((a, b) => a.startTime.CompareTo(b.startTime));
            var clusters = AnimGroupBakery.BuildClusters(sorted, null);
            failed += AssertEqual("overlapping events merge into one cluster",
                clusters.Count.ToString(), "2");
            failed += AssertEqual("cluster keeps both parallel events",
                clusters[0].events.Count.ToString(), "2");
            failed += AssertFloat("cluster end = max event end", clusters[0].end, 4f);

            // 事件时长：rotate/wait 默认值。
            failed += AssertFloat("rotate default duration",
                AnimGroupBakery.EventDuration(
                    new AnimGroupEventDto { type = "rotate" }, null), 2f);
            failed += AssertFloat("wait default duration",
                AnimGroupBakery.EventDuration(
                    new AnimGroupEventDto { type = "wait" }, null), 1f);
            return failed;
        }

        private static int AssertFloat(string label, float actual, float expected)
        {
            if (Mathf.Abs(actual - expected) < 0.001f) return 0;
            Debug.LogError("[LayoutEditor] FAIL " + label + ": expected " + expected + ", got " + actual);
            return 1;
        }
    }

    /// <summary>全屏特效事件：时长默认值 / 时间簇装箱 / 节奏（簇起点差 = 队列延迟）。</summary>
    private static class FxEventTests
    {
        public static int Run()
        {
            int failed = 0;

            // 时长默认值：shake 2s，flash 0.8s（对齐 web 端 eventDuration）。
            failed += AssertFloat("shake default duration",
                AnimGroupBakery.EventDuration(
                    new AnimGroupEventDto { type = "shake" }, null), 2f);
            failed += AssertFloat("flash default duration",
                AnimGroupBakery.EventDuration(
                    new AnimGroupEventDto { type = "flash" }, null), 0.8f);
            failed += AssertFloat("shake explicit duration",
                AnimGroupBakery.EventDuration(
                    new AnimGroupEventDto { type = "shake", duration = 3.5f }, null), 3.5f);

            // 簇装箱：错开的 flash 事件各自成簇（4_2 的 7×Flash 不规则节奏模式），
            // 簇起点差即 TriggerQueue delays。
            var g = new AnimGroupDto
            {
                id = "fx1",
                displayName = "LightningStorm",
                groupKind = "fx",
                events = new[]
                {
                    new AnimGroupEventDto { id = "f1", type = "flash", startTime = 0f, duration = 0.8f },
                    new AnimGroupEventDto { id = "f2", type = "flash", startTime = 6f, duration = 0.8f },
                    new AnimGroupEventDto { id = "f3", type = "flash", startTime = 15f, duration = 0.8f }
                },
                waypoints = new AnimGroupWaypointDto[0]
            };
            var sorted = new System.Collections.Generic.List<AnimGroupEventDto>(g.events);
            sorted.Sort((a, b) => a.startTime.CompareTo(b.startTime));
            var clusters = AnimGroupBakery.BuildClusters(sorted, null);
            failed += AssertEqual("spaced flashes form one cluster each",
                clusters.Count.ToString(), "3");
            failed += AssertFloat("cluster 2 starts 6s in (queue delay)", clusters[1].start, 6f);
            failed += AssertFloat("cluster 3 starts 15s in (queue delay)", clusters[2].start, 15f);

            // 重叠的同类特效事件并入同簇（烘焙时只取首个事件烘 clip）。
            var g2 = new AnimGroupDto
            {
                id = "fx2",
                displayName = "Quake",
                groupKind = "fx",
                events = new[]
                {
                    new AnimGroupEventDto { id = "s1", type = "shake", startTime = 0f, duration = 2f },
                    new AnimGroupEventDto { id = "s2", type = "shake", startTime = 1f, duration = 2f }
                },
                waypoints = new AnimGroupWaypointDto[0]
            };
            var sorted2 = new System.Collections.Generic.List<AnimGroupEventDto>(g2.events);
            sorted2.Sort((a, b) => a.startTime.CompareTo(b.startTime));
            var clusters2 = AnimGroupBakery.BuildClusters(sorted2, null);
            failed += AssertEqual("overlapping shakes merge into one cluster",
                clusters2.Count.ToString(), "1");
            failed += AssertFloat("merged cluster end = max event end", clusters2[0].end, 3f);
            return failed;
        }

        private static int AssertFloat(string label, float actual, float expected)
        {
            if (Mathf.Abs(actual - expected) < 0.001f) return 0;
            Debug.LogError("[LayoutEditor] FAIL " + label + ": expected " + expected + ", got " + actual);
            return 1;
        }
    }

    private static string KeyFor(string displayName, string id, string hierarchyPath)
    {
        return AnimGroupBakery.BuildAssetKey(SceneName, new AnimGroupDto
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
