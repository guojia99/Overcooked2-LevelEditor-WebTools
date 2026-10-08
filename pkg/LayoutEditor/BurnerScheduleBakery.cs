using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using LevelEditorStub;

/// <summary>
/// 燃烧弹射器（Burner / 火焰喷射器）波次时序烘焙（2026-10-08）。
/// web 文档 item.burner.waves 为权威数据；写回时在 Design/Burner Logic/Burner_&lt;key&gt;
/// 下烘焙与参考关 NanyeLevelSet/nanye_level_3 完全一致的原生机制：
///   - 每个落点一个 TriggerTimer（time = 波次累计时间、completeTrigger = SpawnHazard、
///     startTiming = 1、triggerAtStart = 0）——同一波 N 个落点 = N 个同刻 timer
///     （参考关实测：30s×4 / 60×2 / 90×2 / 120×2 / 150×4 / 180×2 / 210×4）；
///   - 一个 TriggerOnObject（SpawnHazard → SpawnHazard，target = Burner 伪预制体根 GO）。
///     Burner.prefab 根自带的 TriggerOnObject 在 Play 期由 PseudoPrefabBurner.Setup
///     接到 childGameObject 上，形成「timer → helper 转发 → 伪根 → child」的触发链，
///     按 stub.targetPositions 顺序逐次发射。
/// 落点本体写 stub.targetPositions（StubIO.ApplyStub flatten，randomTargetOrder=false）。
/// ImportFromScene 从场景 timer 反推 waves（兼容旧式根级 BurnerTrigger；写回接管后删除）。
/// </summary>
public static class BurnerScheduleBakery
{
    public const string RootPath = "Design/Burner Logic";
    public const string HelperPrefix = "Burner_";
    /** 宿主原生触发名（参考关与 Burner.prefab 自带 TriggerOnObject 均用它）。 */
    public const string FireTrigger = "SpawnHazard";
    public const float DefaultIntervalSeconds = 30f;
    /** 旧式手工时序载体：场景根级 BurnerTrigger（参考关的原始做法，写回接管后删除）。 */
    public const string LegacyCarrierName = "BurnerTrigger";

    // ---------------------------------------------------------------- helpers

    private static string Hash8(string s)
    {
        using (var md5 = System.Security.Cryptography.MD5.Create())
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(s ?? "");
            var hash = md5.ComputeHash(bytes);
            var sb = new System.Text.StringBuilder(8);
            for (int i = 0; i < 4; i++) sb.Append(hash[i].ToString("x2"));
            return sb.ToString();
        }
    }

    private static string HelperNameFor(string instanceId)
    {
        return HelperPrefix + Hash8(instanceId ?? "");
    }

    /// <summary>波内发射模式编码进 helper 名（场景即事实源）：
    /// 齐射 = 基础名；顺序 = 基础名 + "~r&lt;stagger&gt;"（如 Burner_ab12~r0.35）。</summary>
    private static string HelperFullName(string baseName, bool sequential, float stagger)
    {
        if (!sequential) return baseName;
        return baseName + "~r" + stagger.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void ParseHelperMode(string helperName, out bool sequential, out float stagger)
    {
        sequential = false;
        stagger = 0.35f;
        var idx = helperName != null ? helperName.IndexOf("~r", System.StringComparison.Ordinal) : -1;
        if (idx < 0) return;
        float v;
        if (float.TryParse(helperName.Substring(idx + 2), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out v) && v >= 0f)
            stagger = v;
        sequential = true;
    }

    /// <summary>按前缀找 helper 子物体（兼容旧/新模式名：Burner_&lt;hash&gt; 或 +~r 后缀），
    /// 返回其 Transform（调用方负责改名到当前模式全名）。</summary>
    private static Transform FindHelper(Transform root, string baseName)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            var c = root.GetChild(i);
            var n = c.name;
            if (n == baseName || n.StartsWith(baseName + "~r", System.StringComparison.Ordinal))
                return c;
        }
        return null;
    }

    /// <summary>顺序模式的波内阅读顺序排序：从上到下（z 升）、从左到右（x 升），
    /// 原序做最终 tiebreak（List.Sort 不稳定，显式索引保证确定性）。
    /// ApplyStub flatten targetPositions 与 BakeSchedule 排 timer 必须共用本排序，
    /// 保证「第 i 发 → target[i]」一一对应。</summary>
    public static LayoutBurnerWavePositionDto[] SortWavePositions(LayoutBurnerWavePositionDto[] positions)
    {
        if (positions == null || positions.Length <= 1) return positions;
        var order = new int[positions.Length];
        for (int i = 0; i < order.Length; i++) order[i] = i;
        var arr = positions;
        System.Array.Sort(order, (a, b) =>
        {
            var za = arr[a].z; var zb = arr[b].z;
            if (za < zb - 0.0001f) return -1;
            if (za > zb + 0.0001f) return 1;
            var xa = arr[a].x; var xb = arr[b].x;
            if (xa < xb - 0.0001f) return -1;
            if (xa > xb + 0.0001f) return 1;
            return a - b;
        });
        var outp = new LayoutBurnerWavePositionDto[positions.Length];
        for (int i = 0; i < order.Length; i++) outp[i] = arr[order[i]];
        return outp;
    }

    private static GameObject ResolveObject(string id, Dictionary<string, GameObject> createdObjects)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (id.StartsWith("u:", StringComparison.Ordinal))
        {
            int iid;
            if (int.TryParse(id.Substring(2), out iid))
                return EditorUtility.InstanceIDToObject(iid) as GameObject;
            return null;
        }
        GameObject go;
        if (createdObjects != null && createdObjects.TryGetValue(id, out go))
            return go;
        return null;
    }

    // ---------------------------------------------------------------- bake

    public static string Sync(Scene scene, LayoutDocumentDto doc, Dictionary<string, GameObject> createdObjects)
    {
        try
        {
            return SyncInner(scene, doc, createdObjects);
        }
        catch (Exception e)
        {
            LayoutEditorLog.LogWarning("burner schedule: bake exception: " + e);
            return "燃烧弹射器时序写回异常：" + e.Message;
        }
    }

    private static string SyncInner(Scene scene, LayoutDocumentDto doc, Dictionary<string, GameObject> createdObjects)
    {
        var items = doc != null && doc.items != null ? doc.items : new LayoutItemDto[0];
        var rootT = LayoutEditorHierarchy.FindOrCreatePath(RootPath);
        if (rootT == null)
            return "燃烧弹射器：无法创建 " + RootPath;

        var usedHelpers = new HashSet<GameObject>();
        var ownedBurners = new HashSet<GameObject>();
        var errors = new List<string>();

        foreach (var item in items)
        {
            if (item == null || item.stubKind != "Burner" || item.burner == null) continue;
            var waves = item.burner.waves;
            // waves == null = 未编排（老场景未动过 / 新放未配置）：不动场景现状。
            if (waves == null) continue;

            var burnerGo = ResolveObject(item.instanceId, createdObjects);
            if (burnerGo == null)
            {
                errors.Add("燃烧弹射器不在场景：" + item.instanceId);
                continue;
            }
            ownedBurners.Add(burnerGo);

            var baseName = HelperNameFor(item.instanceId);
            bool sequential = item.burner.sequentialFire;
            float stagger = item.burner.fireStaggerSeconds > 0f ? item.burner.fireStaggerSeconds : 0.35f;
            var helperT = FindHelper(rootT, baseName);
            GameObject helper;
            if (helperT != null)
            {
                helper = helperT.gameObject;
            }
            else
            {
                helper = new GameObject(baseName);
                Undo.RegisterCreatedObjectUndo(helper, "Layout Editor Burner Schedule");
                helper.transform.SetParent(rootT, false);
            }
            // 模式编码名（齐射/顺序切换时改名；旧名残留由 CleanupStale 兜底删除——
            // 实际不会残留：FindHelper 前缀匹配新旧名）。
            var fullName = HelperFullName(baseName, sequential, stagger);
            if (helper.name != fullName) helper.name = fullName;
            usedHelpers.Add(helper);
            BakeSchedule(helper, burnerGo, waves, item.burner.startDelaySeconds, sequential, stagger);
        }

        CleanupStale(rootT, usedHelpers);
        MigrateLegacyCarriers(scene, ownedBurners);

        if (errors.Count > 0)
            LayoutEditorLog.LogWarning("burner schedule: bake errors: " + string.Join("; ", errors.ToArray()));
        return errors.Count > 0 ? string.Join("; ", errors.ToArray()) : null;
    }

    private static void BakeSchedule(GameObject helper, GameObject burnerGo,
        LayoutBurnerWaveDto[] waves, float startDelaySeconds, bool sequential, float stagger)
    {
        // 转发器：SpawnHazard → Burner 伪根（伪根自带 TriggerOnObject 再转 child）。
        var relays = helper.GetComponents<TriggerOnObject>();
        TriggerOnObject relay = null;
        for (int i = 0; i < relays.Length; i++)
        {
            if (relays[i].m_trigger == FireTrigger) { relay = relays[i]; continue; }
            Undo.DestroyObjectImmediate(relays[i]);
        }
        if (relay == null)
            relay = Undo.AddComponent<TriggerOnObject>(helper);
        else
            Undo.RecordObject(relay, "Layout Editor Burner");
        relay.m_trigger = FireTrigger;
        relay.m_triggerToFire = FireTrigger;
        relay.m_targetObject = burnerGo;
        relay.m_targetObjects = new GameObject[0];
        EditorUtility.SetDirty(relay);

        // 定时器全量重建（每落点一个；空波次跳过且不占时间轴）。
        var oldTimers = helper.GetComponents<TriggerTimer>();
        for (int i = 0; i < oldTimers.Length; i++)
            Undo.DestroyObjectImmediate(oldTimers[i]);

        // 时间模型：开局延迟（默认 10，钳 ≥0）后第一波；后续按各波间隔顺延
        //（waves[0].intervalSeconds 不参与计时）。DTO 字段缺省由 JsonUtility
        // 字段初始化器给 10。波内：齐射 = 同刻；顺序 = 按阅读顺序（从上到下、
        // 从左到右，SortWavePositions 与 ApplyStub flatten 共用）逐发延迟 stagger。
        float startDelay = startDelaySeconds > 0f ? startDelaySeconds : 0f;
        float t = startDelay;
        bool first = true;
        for (int w = 0; w < waves.Length; w++)
        {
            var wave = waves[w];
            if (wave == null) continue;
            var positions = wave.positions;
            if (positions == null || positions.Length == 0) continue;
            if (!first)
                t += wave.intervalSeconds > 0f ? wave.intervalSeconds : DefaultIntervalSeconds;
            first = false;
            var ordered = sequential ? SortWavePositions(positions) : positions;
            for (int p = 0; p < ordered.Length; p++)
            {
                var timer = Undo.AddComponent<TriggerTimer>(helper);
                timer.m_startTrigger = null;
                timer.m_completeTrigger = FireTrigger;
                timer.m_time = sequential ? t + p * stagger : t;
                timer.m_startTiming = true;
                timer.m_triggerAtStart = false;
            }
        }
        EditorUtility.SetDirty(helper);
    }

    private static void CleanupStale(Transform root, HashSet<GameObject> used)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            var child = root.GetChild(i);
            if (!used.Contains(child.gameObject))
                Undo.DestroyObjectImmediate(child.gameObject);
        }
    }

    /// <summary>删除被本次写回接管的旧式根级 BurnerTrigger（其转发目标 = 我们重建过
    /// 时序的 Burner）。Burner.prefab 根自带的 TriggerOnObject 目标是 child 而非伪根，
    /// 不会命中，安全。</summary>
    private static void MigrateLegacyCarriers(Scene scene, HashSet<GameObject> ownedBurners)
    {
        if (ownedBurners.Count == 0) return;
        foreach (var rootGo in scene.GetRootGameObjects())
        {
            if (rootGo.name != LegacyCarrierName) continue;
            var relays = rootGo.GetComponents<TriggerOnObject>();
            for (int i = 0; i < relays.Length; i++)
            {
                if (relays[i].m_trigger != FireTrigger) continue;
                var target = relays[i].m_targetObject;
                if (target != null && ownedBurners.Contains(target))
                {
                    LayoutEditorLog.Log("burner schedule: migrated legacy " + LegacyCarrierName
                        + " -> " + RootPath + "/" + HelperNameFor("u:" + target.GetInstanceID()));
                    Undo.DestroyObjectImmediate(rootGo);
                    break;
                }
            }
        }
    }

    // ---------------------------------------------------------------- import

    /// <summary>场景 → 文档：为每个 Burner 物品从 timer 反推 waves。
    /// 来源优先级：Design/Burner Logic helper（新式）与根级 BurnerTrigger（旧式）
    /// 并列扫描（一个 Burner 只应有一处；若多处，取 timer 总数最多的一处）。
    /// 无 timer 但有 targetPositions 时导出为单波（保持 web 可见可改）。</summary>
    public static void ImportFromScene(Scene scene, List<LayoutItemDto> items)
    {
        if (items == null) return;

        var byGo = new Dictionary<GameObject, LayoutItemDto>();
        foreach (var item in items)
        {
            if (item == null || item.stubKind != "Burner" || item.burner == null) continue;
            var go = ResolveObject(item.instanceId, null);
            if (go != null && !byGo.ContainsKey(go))
                byGo[go] = item;
        }
        if (byGo.Count == 0) return;

        // 收集候选时序载体：新式 helper 子物体（名后缀 ~r&lt;stagger&gt; = 顺序模式）
        // + 旧式根级载体（恒为齐射）。
        var carriers = new List<GameObject>();
        var carrierSequential = new Dictionary<GameObject, bool>();
        var carrierStagger = new Dictionary<GameObject, float>();
        var logicRoot = LayoutEditorHierarchy.FindByPath(RootPath);
        if (logicRoot != null)
        {
            for (int i = 0; i < logicRoot.childCount; i++)
            {
                var child = logicRoot.GetChild(i).gameObject;
                bool seq;
                float stag;
                ParseHelperMode(child.name, out seq, out stag);
                carriers.Add(child);
                carrierSequential[child] = seq;
                carrierStagger[child] = stag;
            }
        }
        foreach (var rootGo in scene.GetRootGameObjects())
        {
            if (rootGo.name == LegacyCarrierName)
            {
                carriers.Add(rootGo);
                carrierSequential[rootGo] = false;
                carrierStagger[rootGo] = 0.35f;
            }
        }

        var imported = new Dictionary<GameObject, LayoutBurnerWaveDto[]>();
        var importedDelay = new Dictionary<GameObject, float>();
        var importedSequential = new Dictionary<GameObject, bool>();
        var importedStagger = new Dictionary<GameObject, float>();
        foreach (var carrier in carriers)
        {
            if (carrier == null) continue;
            var relays = carrier.GetComponents<TriggerOnObject>();
            for (int i = 0; i < relays.Length; i++)
            {
                var relay = relays[i];
                if (relay.m_trigger != FireTrigger) continue;
                var target = relay.m_targetObject;
                if (target == null || !byGo.ContainsKey(target)) continue;

                bool sequential;
                float stagger;
                float startDelay;
                carrierSequential.TryGetValue(carrier, out sequential);
                carrierStagger.TryGetValue(carrier, out stagger);
                var waves = BuildWaves(carrier, target, out startDelay, sequential, stagger);
                if (waves == null || waves.Length == 0) continue;
                LayoutBurnerWaveDto[] prev;
                if (imported.TryGetValue(target, out prev) && prev.Length >= waves.Length)
                    continue;
                imported[target] = waves;
                importedDelay[target] = startDelay;
                importedSequential[target] = sequential;
                importedStagger[target] = stagger;
            }
        }

        foreach (var kv in imported)
        {
            var item = byGo[kv.Key];
            item.burner.waves = kv.Value;
            item.burner.startDelaySeconds = importedDelay[kv.Key];
            item.burner.sequentialFire = importedSequential[kv.Key];
            item.burner.fireStaggerSeconds = importedStagger[kv.Key];
            // 波次语义依赖顺序发射，导入即固化。
            item.burner.randomTargetOrder = false;
        }

        // 兜底：无任何 timer 但有落点 → 单波（开局延迟走默认 10，齐射），保证 web 可见可编辑。
        foreach (var kv in byGo)
        {
            if (imported.ContainsKey(kv.Key)) continue;
            var stub = kv.Key.GetComponent<PseudoPrefabBurnerStub>();
            var flat = stub != null ? stub.targetPositions : null;
            if (flat == null || flat.Length == 0) continue;
            kv.Value.burner.waves = new LayoutBurnerWaveDto[]
            {
                new LayoutBurnerWaveDto
                {
                    intervalSeconds = DefaultIntervalSeconds,
                    positions = ToPositions(flat, 0, flat.Length),
                },
            };
            kv.Value.burner.startDelaySeconds = 10f;
            kv.Value.burner.sequentialFire = false;
            kv.Value.burner.fireStaggerSeconds = 0.35f;
            kv.Value.burner.randomTargetOrder = false;
        }
    }

    private static LayoutBurnerWaveDto[] BuildWaves(GameObject carrier, GameObject burnerGo,
        out float startDelay, bool sequential, float stagger)
    {
        startDelay = 10f;
        var timers = carrier.GetComponents<TriggerTimer>();
        // 仅统计发 SpawnHazard 的计时器；0.01s 精度分组（时间升序）——顺序模式
        // 逐发间隔 0.35s 级，0.1s 精度会把 10.35 吞成 10.4。
        var times = new List<float>();
        for (int i = 0; i < timers.Length; i++)
        {
            if (timers[i].m_completeTrigger != FireTrigger) continue;
            if (!timers[i].m_startTiming) continue;
            var t = Mathf.Round(Mathf.Max(0f, timers[i].m_time) * 100f) / 100f;
            times.Add(t);
        }
        if (times.Count == 0) return null;
        times.Sort();

        var stub = burnerGo.GetComponent<PseudoPrefabBurnerStub>();
        var flat = stub != null ? stub.targetPositions : null;
        if (flat == null) flat = new Vector3[0];

        // 先按同刻分组 → (time, count, position 切片游标)。
        var groups = new List<GroupEntry>();
        int idx = 0;
        while (idx < times.Count)
        {
            float t = times[idx];
            int count = 1;
            while (idx + count < times.Count && Mathf.Approximately(times[idx + count], t))
                count++;
            groups.Add(new GroupEntry { time = t, count = count, first = idx });
            idx += count;
        }

        var waves = new List<LayoutBurnerWaveDto>();
        int consumed = 0;
        float lastWaveStart = 0f;
        int g = 0;
        while (g < groups.Count)
        {
            var grp = groups[g];
            int take = Mathf.Min(grp.count, flat.Length - consumed);
            if (take <= 0)
            {
                g++;
                continue;
            }
            var positions = new List<LayoutBurnerWavePositionDto>(ToPositions(flat, consumed, take));
            consumed += take;
            int next = g + 1;
            // 顺序模式：后续单落点组与前一组的差 = stagger → 并入本波（逐发）。
            if (sequential)
            {
                while (next < groups.Count
                    && groups[next].count == 1
                    && consumed < flat.Length
                    && Mathf.Abs(groups[next].time - groups[next - 1].time - stagger) <= 0.05f)
                {
                    positions.Add(ToPositions(flat, consumed, 1)[0]);
                    consumed++;
                    next++;
                }
            }
            // 首波时间 = 开局延迟；后续波间隔 = 与上一波开始时间差。
            if (waves.Count == 0) startDelay = grp.time;
            waves.Add(new LayoutBurnerWaveDto
            {
                intervalSeconds = waves.Count == 0
                    ? DefaultIntervalSeconds
                    : Mathf.Max(0.1f, grp.time - lastWaveStart),
                positions = positions.ToArray(),
            });
            lastWaveStart = grp.time;
            g = next;
        }
        return waves.ToArray();
    }

    private struct GroupEntry
    {
        public float time;
        public int count;
        public int first;
    }

    private static LayoutBurnerWavePositionDto[] ToPositions(Vector3[] flat, int start, int count)
    {
        var outp = new LayoutBurnerWavePositionDto[count];
        for (int i = 0; i < count; i++)
        {
            outp[i] = new LayoutBurnerWavePositionDto
            {
                x = flat[start + i].x,
                z = flat[start + i].z,
            };
        }
        return outp;
    }
}
