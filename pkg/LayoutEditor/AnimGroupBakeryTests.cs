using System;
using UnityEditor;
using UnityEngine;

/// <summary>Editor-time checks for AnimGroupBakery asset key generation.</summary>
public static class AnimGroupBakeryTests
{
    private const string SceneName = "s_jia_level_1_3";

    [MenuItem("Layout Editor/测试 (Tests)/运行动画组烘焙器测试 (Run AnimGroupBakery Tests)", false, 600)]
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
        failed += NewEventTests.Run();

        if (failed == 0)
            Debug.Log("[LayoutEditor/Tests] 动画组烘焙器测试：全部通过。");
        else
            Debug.LogError("[LayoutEditor/Tests] 动画组烘焙器测试：" + failed + " 项失败。");
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
            Debug.LogError("[LayoutEditor/Tests] 失败 " + label + "：期望 " + expected + "，实际 " + actual);
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
            Debug.LogError("[LayoutEditor/Tests] 失败 " + label + "：期望 " + expected + "，实际 " + actual);
            return 1;
        }
    }

    /// <summary>新增事件类型（scale/tilt/orbit）与缓动/贝塞尔采样层的关键值断言。
    /// EaseValue 与 web 端 easeValue 同名同公式（flashEnvelope 铁律），关键点值
    /// 改动须两端同步。</summary>
    private static class NewEventTests
    {
        public static int Run()
        {
            int failed = 0;

            // ---- EventDuration 新类型默认值（与 web eventDuration 对齐）。
            failed += AssertFloat("scale default duration",
                AnimGroupBakery.EventDuration(new AnimGroupEventDto { type = "scale" }, null), 1f);
            failed += AssertFloat("tilt default duration",
                AnimGroupBakery.EventDuration(new AnimGroupEventDto { type = "tilt" }, null), 1f);
            failed += AssertFloat("orbit default duration",
                AnimGroupBakery.EventDuration(new AnimGroupEventDto { type = "orbit" }, null), 8f);
            failed += AssertFloat("orbit explicit duration",
                AnimGroupBakery.EventDuration(
                    new AnimGroupEventDto { type = "orbit", orbitSeconds = 4.5f }, null), 4.5f);
            failed += AssertFloat("fade default duration",
                AnimGroupBakery.EventDuration(new AnimGroupEventDto { type = "fade" }, null), 1f);
            failed += AssertFloat("fade explicit duration",
                AnimGroupBakery.EventDuration(
                    new AnimGroupEventDto { type = "fade", fadeSeconds = 2.5f }, null), 2.5f);

            // ---- EaseValue 关键点（全部缓动 f(0)=0、f(1)=1）。
            string[] easings = { "linear", "easeIn", "easeOut", "easeInOut", "easeOutBounce", "easeInOutBack" };
            foreach (var ez in easings)
            {
                failed += AssertFloat(ez + " f(0)=0", AnimGroupBakery.EaseValue(0f, ez), 0f);
                failed += AssertFloat(ez + " f(1)=1", AnimGroupBakery.EaseValue(1f, ez), 1f);
            }
            failed += AssertFloat("easeIn f(0.5)=0.25",
                AnimGroupBakery.EaseValue(0.5f, "easeIn"), 0.25f);
            failed += AssertFloat("easeOut f(0.5)=0.75",
                AnimGroupBakery.EaseValue(0.5f, "easeOut"), 0.75f);
            failed += AssertFloat("easeInOut f(0.5)=0.5",
                AnimGroupBakery.EaseValue(0.5f, "easeInOut"), 0.5f);
            failed += AssertFloat("easeOutBounce f(0.5)=0.765625",
                AnimGroupBakery.EaseValue(0.5f, "easeOutBounce"), 0.765625f);
            failed += AssertFloat("easeInOutBack f(0.5)=0.5",
                AnimGroupBakery.EaseValue(0.5f, "easeInOutBack"), 0.5f);
            failed += AssertFloat("null easing falls back to linear",
                AnimGroupBakery.EaseValue(0.5f, null), 0.5f);

            // ---- OrbitPos：Unity Y 轴约定（+deg 绕 up 把 +X 转向 -Z；cw/ccw 同 rotate）。
            var cw = AnimGroupBakery.OrbitPos(new Vector2(2f, 0f), Vector2.zero, 90f, "cw");
            failed += AssertFloat("orbit cw 90° x", cw.x, 0f);
            failed += AssertFloat("orbit cw 90° z", cw.y, -2f);
            var ccw = AnimGroupBakery.OrbitPos(new Vector2(2f, 0f), Vector2.zero, 90f, "ccw");
            failed += AssertFloat("orbit ccw 90° x", ccw.x, 0f);
            failed += AssertFloat("orbit ccw 90° z", ccw.y, 2f);
            // 整圈回到起点（loop clip 首尾闭合的前提）。
            var full = AnimGroupBakery.OrbitPos(new Vector2(1.5f, -0.7f), new Vector2(0.3f, 0.4f), 360f, "cw");
            failed += AssertFloat("orbit 360° returns to start (x)", full.x, 1.5f);
            failed += AssertFloat("orbit 360° returns to start (z)", full.y, -0.7f);

            // ---- 新类型参与时间簇装箱（EventDuration 驱动簇区间）。
            var g = new AnimGroupDto
            {
                id = "n1",
                displayName = "NewTypes",
                events = new[]
                {
                    new AnimGroupEventDto { id = "o", type = "orbit", startTime = 0f, orbitSeconds = 4f },
                    new AnimGroupEventDto { id = "t", type = "tilt", startTime = 1f, tiltSeconds = 2f },
                    new AnimGroupEventDto { id = "s", type = "scale", startTime = 4.5f, scaleSeconds = 1f }
                },
                waypoints = new AnimGroupWaypointDto[0]
            };
            var sorted = new System.Collections.Generic.List<AnimGroupEventDto>(g.events);
            sorted.Sort((a, b) => a.startTime.CompareTo(b.startTime));
            var clusters = AnimGroupBakery.BuildClusters(sorted, null);
            failed += AssertEqual("orbit+tilt overlap merges, scale starts new cluster",
                clusters.Count.ToString(), "2");
            failed += AssertFloat("first cluster end = orbit end", clusters[0].end, 4f);
            failed += AssertFloat("scale cluster starts after orbit", clusters[1].start, 4.5f);
            return failed;
        }

        private static int AssertFloat(string label, float actual, float expected)
        {
            if (Mathf.Abs(actual - expected) < 0.001f) return 0;
            Debug.LogError("[LayoutEditor/Tests] 失败 " + label + "：期望 " + expected + "，实际 " + actual);
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
        Debug.LogError("[LayoutEditor/Tests] 失败 " + label + "：期望 \"" + expected +
            "\"，实际 \"" + actual + "\"");
        return 1;
    }

    private static int AssertNotEqual(string label, string a, string b)
    {
        if (!string.Equals(a, b, StringComparison.Ordinal))
            return 0;
        Debug.LogError("[LayoutEditor/Tests] 失败 " + label + "：两个 key 相同 \"" + a + "\"");
        return 1;
    }
}
