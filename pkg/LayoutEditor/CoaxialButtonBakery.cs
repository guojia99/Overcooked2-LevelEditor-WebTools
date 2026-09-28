using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using LevelEditorStub;

/// <summary>
/// 同轴按钮组烘焙（2026-09-28）。在 Design/Coaxial Logic/Coax_&lt;key&gt; 下创建
/// 隐藏逻辑物体，挂 CustomStub.CoaxialButtonGroup（反射）+ Coaxial| 自愈 tag：
///  - 独立根目录（不放 Design/Button Logic——ButtonLinkBakery.CleanupStale 会
///    删除该根下不在其 usedHelpers 里的全部子物体）；
///  - 无 Animator/controller 资产（比 BLRelay 简单一档）：按压检测走
///    SwitchReenable 同款轮询，组件自含全部状态机；
///  - 成员按钮复用 ButtonLink 的 _BL&lt;n&gt; 唯一命名 + 停用 SwitchReenable
///    自动复位（回绿由 CoaxialButtonGroup 统一投递）；
///  - 场景是唯一事实源：ImportFromScene 从组件序列化字段（权威）还原文档数据，
///    tag 为真机自愈载体（编辑器烘活组件在真机是 Missing Script 死件）。
/// tag 格式（与 EntryPoint.HealCoaxialButtonGroup 对称，%,;>| 做 %XX 转义）：
///   Coaxial|W:&lt;window&gt;|N:&lt;按钮根名,..&gt;|T:&lt;目标名&gt;,&lt;触发&gt;;..
/// </summary>
public static class CoaxialButtonBakery
{
    public const string RootPath = "Design/Coaxial Logic";
    public const string HelperPrefix = "Coax_";

    private const string ImportedMarker = "scene:Coaxial:";

    // ------------------------------------------------------------------ naming

    /// <summary>同轴组 helper 名（对回导 link 幂等：已含前缀则原样返回）。</summary>
    private static string HelperNameFor(LayoutCoaxialLinkDto link)
    {
        var id = link != null ? link.id ?? "" : "";
        if (id.StartsWith(ImportedMarker, StringComparison.Ordinal))
            return id.Substring(ImportedMarker.Length);
        return HelperPrefix + ButtonLinkBakery.Hash8(id);
    }

    /// <summary>目标机器默认触发名：断头台 Chop / 大炮 Launch / 其余（饮料、
    /// 酱料机）Next——与前端 linkSwitch 同款规则（机器包装 prefab 自带
    /// TriggerOnObject 翻译层，自定义名真机不响应）。</summary>
    internal static string DefaultTriggerFor(GameObject target)
    {
        var n = target != null ? target.name.ToLowerInvariant() : "";
        if (n.Contains("guillotine")) return "Chop";
        if (n.Contains("cannon")) return "Launch";
        return "Next";
    }

    // ------------------------------------------------------------------ bake

    public static string Sync(Scene scene, LayoutDocumentDto doc,
        Dictionary<string, GameObject> createdObjects)
    {
        try
        {
            return SyncInner(scene, doc, createdObjects);
        }
        catch (Exception e)
        {
            LayoutEditorLog.LogWarning("coaxial: bake exception: " + e);
            return "同轴按钮写回异常：" + e.Message;
        }
    }

    private static string SyncInner(Scene scene, LayoutDocumentDto doc,
        Dictionary<string, GameObject> createdObjects)
    {
        var errors = new List<string>();
        var usedHelpers = new HashSet<string>(StringComparer.Ordinal);

        var links = doc != null && doc.coaxialLinks != null && doc.coaxialLinks.links != null
            ? doc.coaxialLinks.links
            : new LayoutCoaxialLinkDto[0];

        // ---- 联动源预处理（与 ButtonLink 同款）：唯一命名 + 停用自动复位。
        // 命名必须先于组件字段/tag 写入（GameObject.Find 身份键 = GO 名）。
        var sources = new List<GameObject>();
        var seen = new HashSet<int>();
        foreach (var link in links)
        {
            if (link == null || link.sourceIds == null) continue;
            foreach (var sid in link.sourceIds)
                CollectSource(sid, createdObjects, sources, seen);
        }
        if (sources.Count > 0)
        {
            int renamed = ButtonLinkBakery.EnsureUniqueLinkSourceNames(sources);
            int disarmed = 0;
            for (int i = 0; i < sources.Count; i++)
            {
                if (sources[i] != null && LayoutEditorStubIO.DisarmSwitchReenable(sources[i]))
                    disarmed++;
            }
            if (renamed > 0 || disarmed > 0)
                LayoutEditorLog.Log("coaxial: 联动源唯一命名 × " + renamed +
                    "、停用自动复位 × " + disarmed);
        }

        // 直连联动冲突告警用：文档 switchLinks 里已登记的开关 id。
        var directWired = new HashSet<string>(StringComparer.Ordinal);
        if (doc != null && doc.switchLinks != null)
        {
            foreach (var sl in doc.switchLinks)
            {
                if (sl != null && !string.IsNullOrEmpty(sl.switchId))
                    directWired.Add(sl.switchId);
            }
        }

        foreach (var link in links)
        {
            if (link == null || link.sourceIds == null) continue;
            var helperName = HelperNameFor(link);

            var members = new List<GameObject>();
            foreach (var sid in link.sourceIds)
            {
                var go = ButtonLinkBakery.ResolveObject(sid, createdObjects);
                if (go == null)
                {
                    errors.Add("同轴按钮：成员不在场景中 " + sid);
                    continue;
                }
                if (!members.Contains(go)) members.Add(go);
            }
            if (members.Count < 2)
            {
                // 解析失败 ≠ 删除：既有 helper（上一轮写回烘焙的正确状态）必须保留，
                // 否则 CleanupStale 会把完好的同轴配置连带删掉。保留 + 报错，用户
                // 重载场景刷新画布（拿到新 u: id）后写回即可恢复重烘焙。
                var existing = LayoutEditorHierarchy.FindByPath(RootPath + "/" + helperName);
                if (existing != null)
                {
                    usedHelpers.Add(helperName);
                    LayoutEditorLog.LogWarning("coaxial: 组「" + helperName +
                        "」成员解析失败，保留既有 helper 不重烘焙（重载场景刷新画布后写回可修复）");
                }
                errors.Add("同轴按钮：组「" + link.id + "」有效成员不足 2 个（" +
                    members.Count + "），已跳过");
                continue;
            }

            var targets = new List<GameObject>();
            var triggers = new List<string>();
            if (link.targetIds != null)
            {
                for (int i = 0; i < link.targetIds.Length; i++)
                {
                    var target = ButtonLinkBakery.ResolveObject(link.targetIds[i], createdObjects);
                    if (target == null)
                    {
                        LayoutEditorLog.LogWarning("coaxial: 目标不在场景中 " +
                            link.targetIds[i] + "，跳过该目标");
                        continue;
                    }
                    targets.Add(target);
                    var trig = link.triggers != null && i < link.triggers.Length
                        ? (link.triggers[i] ?? "").Trim()
                        : "";
                    if (trig.Length == 0) trig = DefaultTriggerFor(target);
                    triggers.Add(trig);
                }
            }

            foreach (var sid in link.sourceIds)
            {
                if (directWired.Contains(sid))
                    LayoutEditorLog.LogWarning("coaxial: 成员 " + sid +
                        " 同时配置了直连联动（switchLinks）——单按即直发会破坏同轴语义，请二选一");
            }

            var helper = EnsureHelper(helperName);
            if (helper == null)
            {
                errors.Add("同轴按钮：无法创建 " + RootPath + "/" + helperName);
                continue;
            }
            usedHelpers.Add(helperName);

            var window = Mathf.Clamp(
                link.windowSeconds > 0f ? link.windowSeconds : 1f,
                CoaxialWindowMin, CoaxialWindowMax);

            AttachComponent(helper.gameObject, members, window, targets, triggers);
            WriteCoaxialTag(helper.gameObject, members, window, targets, triggers);

            LayoutEditorLog.Log("coaxial: helper " + helperName + " 烘焙完成：" +
                members.Count + " 按钮｜窗口 " + window.ToString("0.##") + "s｜目标 " +
                targets.Count + " 个（" + string.Join(",", triggers.ToArray()) + "）");
        }

        CleanupStale(usedHelpers);

        if (errors.Count > 0)
            LayoutEditorLog.LogWarning("coaxial: bake errors: " + string.Join("; ", errors.ToArray()));
        return errors.Count > 0 ? string.Join("; ", errors.ToArray()) : null;
    }

    private const float CoaxialWindowMin = 0.35f;
    private const float CoaxialWindowMax = 5f;

    private static void CollectSource(string sid,
        Dictionary<string, GameObject> createdObjects,
        List<GameObject> sources, HashSet<int> seen)
    {
        if (string.IsNullOrEmpty(sid)) return;
        var go = ButtonLinkBakery.ResolveObject(sid, createdObjects);
        if (go == null) return;
        if (seen.Add(go.GetInstanceID()))
            sources.Add(go);
    }

    private static Transform EnsureHelper(string helperName)
    {
        var t = LayoutEditorHierarchy.FindOrCreatePath(RootPath + "/" + helperName);
        if (t != null)
        {
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
        }
        return t;
    }

    /// <summary>挂/重建 CoaxialButtonGroup（反射；真机烘焙件为 Missing Script，
    /// 由 tag 自愈补挂——编辑器 Play 则本组件直接存活运行）。</summary>
    private static void AttachComponent(GameObject helper, List<GameObject> members,
        float window, List<GameObject> targets, List<string> triggers)
    {
        var type = LayoutEditorStubIO.FindCustomStubType(helper, "CoaxialButtonGroup");
        if (type == null)
        {
            LayoutEditorLog.LogWarning("coaxial: 找不到 CustomStub.CoaxialButtonGroup" +
                "（WebCustomStubRuntime 未编译？）——真机将由 tag 自愈兜底，编辑器 Play 本次无效");
            return;
        }
        foreach (var old in helper.GetComponents(type))
            Undo.DestroyObjectImmediate(old);
        var comp = Undo.AddComponent(helper, type);
        SetField(comp, "m_buttonRootNames", ArrayConvert(members));
        SetFieldFloat(comp, "m_windowSeconds", window);
        SetField(comp, "m_targetNames", ArrayConvert(targets));
        SetField(comp, "m_targetTriggers", triggers.ToArray());
        EditorUtility.SetDirty(comp);
    }

    private static string[] ArrayConvert(List<GameObject> list)
    {
        var arr = new string[list.Count];
        for (int i = 0; i < list.Count; i++)
            arr[i] = list[i].name;
        return arr;
    }

    private static void SetField(Component comp, string field, string[] value)
    {
        var f = comp.GetType().GetField(field, System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance);
        if (f != null)
        {
            Undo.RecordObject(comp, "Layout Editor Coaxial Button");
            f.SetValue(comp, value);
        }
    }

    private static void SetFieldFloat(Component comp, string field, float value)
    {
        var f = comp.GetType().GetField(field, System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance);
        if (f != null)
        {
            Undo.RecordObject(comp, "Layout Editor Coaxial Button");
            f.SetValue(comp, value);
        }
    }

    /// <summary>写 Coaxial| 自愈 tag（单组件约定：helper 上无其他 tag，可整串覆写）。
    /// 浮点 invariant（解析端同约定）。</summary>
    private static void WriteCoaxialTag(GameObject helper, List<GameObject> members,
        float window, List<GameObject> targets, List<string> triggers)
    {
        try
        {
            var sb = new System.Text.StringBuilder("Coaxial");
            sb.Append("|W:").Append(window.ToString("0.###",
                System.Globalization.CultureInfo.InvariantCulture));
            sb.Append("|N:").Append(ButtonLinkBakery.JoinEsc(ArrayConvert(members), ','));
            sb.Append("|T:");
            for (int i = 0; i < targets.Count; i++)
            {
                if (i > 0) sb.Append(';');
                sb.Append(ButtonLinkBakery.EscTag(targets[i].name))
                  .Append(',')
                  .Append(ButtonLinkBakery.EscTag(triggers[i]));
            }
            var tag = helper.GetComponent<LevelEditorStub.SpecificPseudoPrefabTag>();
            if (tag == null)
                tag = Undo.AddComponent<LevelEditorStub.SpecificPseudoPrefabTag>(helper);
            else
                Undo.RecordObject(tag, "Layout Editor Coaxial Tag");
            tag.prefabTag = sb.ToString();
            EditorUtility.SetDirty(tag);
        }
        catch (Exception ex)
        {
            LayoutEditorLog.LogWarning("coaxial: tag 写入失败 " + helper.name + ": " + ex.Message);
        }
    }

    // ---------------------------------------------------------------- cleanup

    /// <summary>删除文档中已不存在的 Coax_* helper（文档权威；空文档=全清）。</summary>
    private static void CleanupStale(HashSet<string> usedHelpers)
    {
        var root = LayoutEditorHierarchy.FindByPath(RootPath);
        if (root == null) return;
        var stale = new List<GameObject>();
        for (int i = 0; i < root.childCount; i++)
        {
            var c = root.GetChild(i);
            if (!usedHelpers.Contains(c.name))
                stale.Add(c.gameObject);
        }
        foreach (var go in stale)
        {
            LayoutEditorLog.Log("coaxial: cleanup stale helper " + go.name);
            Undo.DestroyObjectImmediate(go);
        }
        if (root.childCount == 0 && stale.Count > 0)
            Undo.DestroyObjectImmediate(root.gameObject);
    }

    // ----------------------------------------------------------------- import

    /// <summary>从场景重建同轴组文档数据（场景是唯一事实源）：优先组件序列化
    /// 字段（权威），组件缺失时回落 tag 解析（旧场景兜底）。成员/目标按 GO 名
    /// 反查 "u:&lt;instanceID&gt;"。</summary>
    public static List<LayoutCoaxialLinkDto> ImportFromScene(Scene scene)
    {
        var result = new List<LayoutCoaxialLinkDto>();
        if (!scene.IsValid()) return result;
        var root = LayoutEditorHierarchy.FindByPath(RootPath);
        if (root == null) return result;

        for (int i = 0; i < root.childCount; i++)
        {
            var helper = root.GetChild(i);
            var name = helper.name;
            if (string.IsNullOrEmpty(name) ||
                !name.StartsWith(HelperPrefix, StringComparison.Ordinal))
                continue;

            string[] buttonNames;
            float window;
            string[] targetNames;
            string[] targetTriggers;
            if (!ReadConfig(helper.gameObject, out buttonNames, out window,
                out targetNames, out targetTriggers))
                continue;

            var sourceIds = NamesToRefs(buttonNames, name, "按钮");
            if (sourceIds.Count < 2)
            {
                LayoutEditorLog.LogWarning("coaxial: helper " + name +
                    " 有效成员不足 2 个，跳过回导");
                continue;
            }
            var targetIds = NamesToRefs(targetNames, name, "目标");
            var triggers = new List<string>();
            for (int t = 0; t < targetIds.Count; t++)
                triggers.Add(t < targetTriggers.Length ? targetTriggers[t] : "Chop");

            result.Add(new LayoutCoaxialLinkDto
            {
                id = ImportedMarker + name,
                sourceIds = sourceIds.ToArray(),
                windowSeconds = window,
                targetIds = targetIds.ToArray(),
                triggers = triggers.ToArray(),
            });
        }
        return result;
    }

    /// <summary>读 helper 配置：组件序列化字段（权威）→ tag 回落。</summary>
    private static bool ReadConfig(GameObject helperGo, out string[] buttonNames,
        out float window, out string[] targetNames, out string[] targetTriggers)
    {
        buttonNames = null;
        window = 1f;
        targetNames = new string[0];
        targetTriggers = new string[0];

        var type = LayoutEditorStubIO.FindCustomStubType(helperGo, "CoaxialButtonGroup");
        var comp = type != null ? helperGo.GetComponent(type) : null;
        if (comp != null)
        {
            buttonNames = GetFieldStrings(comp, "m_buttonRootNames");
            window = GetFieldFloat(comp, "m_windowSeconds", 1f);
            targetNames = GetFieldStrings(comp, "m_targetNames");
            targetTriggers = GetFieldStrings(comp, "m_targetTriggers");
            return buttonNames != null && buttonNames.Length >= 2;
        }

        // tag 回落（真机旧场景/组件丢失兜底）
        var tag = helperGo.GetComponent<LevelEditorStub.SpecificPseudoPrefabTag>();
        var tagStr = tag != null ? tag.prefabTag : null;
        if (string.IsNullOrEmpty(tagStr) || !tagStr.StartsWith("Coaxial|", StringComparison.Ordinal))
            return false;
        try
        {
            var parts = tagStr.Split('|');
            string w = null, n = null, t = null;
            for (int i = 1; i < parts.Length; i++)
            {
                if (parts[i].StartsWith("W:", StringComparison.Ordinal)) w = parts[i].Substring(2);
                else if (parts[i].StartsWith("N:", StringComparison.Ordinal)) n = parts[i].Substring(2);
                else if (parts[i].StartsWith("T:", StringComparison.Ordinal)) t = parts[i].Substring(2);
            }
            buttonNames = SplitSimple(n, ',');
            float parsed;
            if (!float.TryParse(w, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out parsed))
                parsed = 1f;
            window = Mathf.Clamp(parsed, CoaxialWindowMin, CoaxialWindowMax);
            var names = new List<string>();
            var trigs = new List<string>();
            foreach (var entry in SplitSimple(t, ';'))
            {
                var pair = entry.Split(',');
                if (pair.Length < 2) continue;
                names.Add(pair[0]);
                trigs.Add(pair[1]);
            }
            targetNames = names.ToArray();
            targetTriggers = trigs.ToArray();
            return buttonNames.Length >= 2;
        }
        catch (Exception ex)
        {
            LayoutEditorLog.LogWarning("coaxial: tag 解析失败 " + helperGo.name + ": " + ex.Message);
            return false;
        }
    }

    /// <summary>tag 侧简化拆分（烘焙侧写入前已转义，正常不含保留字符；仅还原
    /// 可能的转义序列）。</summary>
    private static string[] SplitSimple(string joined, char sep)
    {
        if (string.IsNullOrEmpty(joined)) return new string[0];
        var parts = joined.Split(sep);
        for (int i = 0; i < parts.Length; i++)
            parts[i] = parts[i].Replace("%2C", ",").Replace("%3B", ";")
                .Replace("%3E", ">").Replace("%7C", "|").Replace("%25", "%");
        return parts;
    }

    private static string[] GetFieldStrings(Component comp, string field)
    {
        var f = comp.GetType().GetField(field, System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance);
        return f != null ? f.GetValue(comp) as string[] : null;
    }

    private static float GetFieldFloat(Component comp, string field, float fallback)
    {
        var f = comp.GetType().GetField(field, System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance);
        if (f == null) return fallback;
        try
        {
            return (float)f.GetValue(comp);
        }
        catch
        {
            return fallback;
        }
    }

    /// <summary>GO 名 → 文档引用 id（"u:&lt;instanceID&gt;"；找不到告警跳过）。</summary>
    private static List<string> NamesToRefs(string[] names, string helperName, string kind)
    {
        var refs = new List<string>();
        if (names == null) return refs;
        foreach (var n in names)
        {
            if (string.IsNullOrEmpty(n)) continue;
            var go = GameObject.Find(n);
            if (go == null)
            {
                LayoutEditorLog.LogWarning("coaxial: helper " + helperName + " 的" +
                    kind + " " + n + " 反查失败（被删/改名），跳过");
                continue;
            }
            var id = "u:" + go.GetInstanceID();
            if (!refs.Contains(id)) refs.Add(id);
        }
        return refs;
    }
}
