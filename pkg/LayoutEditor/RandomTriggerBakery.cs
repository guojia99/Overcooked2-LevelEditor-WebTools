using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using LevelEditorStub;

/// <summary>
/// 随机触发器烘焙（3.9.0）。在 Design/RandomTrigger Logic/RT_&lt;hash8&gt; 下创建
/// 隐藏逻辑物体，挂 CustomStub.RandomTrigger（反射）+ RandomTrigger| 自愈 tag：
///  - 独立根目录（不放 Design/Button Logic——ButtonLinkBakery.CleanupStale 会
///    删除该根下不在其 usedHelpers 里的全部子物体）；
///  - 无 Animator/controller 资产（比 Coaxial 再简一档）：源按钮按压检测走
///    TriggerDisableScript.enabled 下降沿轮询（组件自含），按钮回绿由既有
///    SwitchReenable 自动复位链管理（本烘焙不停用、不接管按钮生命周期）；
///  - 每候选独立目标：场景物品伪根 / 其他随机触发器节点（targetRef
///    "rt:&lt;linkId&gt;"）/ 空 = 发给节点自身；
///  - 场景是唯一事实源：ImportFromScene 从组件序列化字段（权威）还原文档数据，
///    tag 为真机自愈载体（编辑器烘活组件在真机是 Missing Script 死件）。
/// tag 格式（与 EntryPoint.ParseRandomTrigger 对称；%,;&gt;|/@ 做 %XX 转义，
/// 比 BLRelay 集（%,;&gt;|）多 '/' 与 '@'——参数 spec 以二者做结构分隔）：
///   RandomTrigger|I:&lt;输入&gt;|C:&lt;冷却&gt;|S:&lt;分隔符&gt;|N:&lt;按钮,..&gt;
///   |E:&lt;信号&gt;,&lt;权重&gt;,&lt;目标&gt;[,&lt;参数spec&gt;];..
/// 参数 spec：f 固定 / p候选/候选 / s长度[@字符集] / d位数 / r最小/最大/小数位。
/// </summary>
public static class RandomTriggerBakery
{
    public const string RootPath = "Design/RandomTrigger Logic";
    public const string HelperPrefix = "RT_";

    private const string ImportedMarker = "scene:RT:";

    // ------------------------------------------------------------------ naming

    /// <summary>RT helper 名（对回导 link 幂等：已含前缀则原样返回）。</summary>
    private static string HelperNameFor(LayoutRandomTriggerLinkDto link)
    {
        var id = link != null ? link.id ?? "" : "";
        if (id.StartsWith(ImportedMarker, StringComparison.Ordinal))
            return id.Substring(ImportedMarker.Length);
        return HelperPrefix + ButtonLinkBakery.Hash8(id);
    }

    /// <summary>按 link id 解析 helper 名（rt: 目标引用用；与 HelperNameFor 同规）。</summary>
    private static string HelperNameForLinkId(string linkId)
    {
        if (string.IsNullOrEmpty(linkId)) return "";
        if (linkId.StartsWith(ImportedMarker, StringComparison.Ordinal))
            return linkId.Substring(ImportedMarker.Length);
        return HelperPrefix + ButtonLinkBakery.Hash8(linkId);
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
            LayoutEditorLog.LogWarning("random trigger: bake exception: " + e);
            return "随机触发器写回异常：" + e.Message;
        }
    }

    private static string SyncInner(Scene scene, LayoutDocumentDto doc,
        Dictionary<string, GameObject> createdObjects)
    {
        var errors = new List<string>();
        var usedHelpers = new HashSet<string>(StringComparer.Ordinal);

        var links = doc != null && doc.randomTriggers != null && doc.randomTriggers.links != null
            ? doc.randomTriggers.links
            : new LayoutRandomTriggerLinkDto[0];

        // ---- 源按钮唯一命名（GameObject.Find 身份键；不动 SwitchReenable——
        // RT 不接管按钮生命周期，回绿由既有自动复位链负责）。
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
            if (renamed > 0)
                LayoutEditorLog.Log("random trigger: 联动源唯一命名 × " + renamed);
        }

        // linkId → helper 名索引（rt: 目标引用解析用）。
        var helperNamesById = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var link in links)
        {
            if (link == null || string.IsNullOrEmpty(link.id)) continue;
            helperNamesById[link.id] = HelperNameFor(link);
        }

        // ---- pass 1：先确保全部 helper 物体存在（跨节点 rt: 引用可解析）。
        var ensuredHelpers = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        foreach (var link in links)
        {
            if (link == null || string.IsNullOrEmpty(link.id)) continue;
            var helperName = helperNamesById[link.id];
            var helper = EnsureHelper(helperName);
            if (helper == null)
            {
                errors.Add("随机触发器：无法创建 " + RootPath + "/" + helperName);
                continue;
            }
            usedHelpers.Add(helperName);
            ensuredHelpers[helperName] = helper.gameObject;
        }

        // ---- pass 2：逐 link 解析配置 → 挂组件 + 写 tag。
        foreach (var link in links)
        {
            if (link == null || string.IsNullOrEmpty(link.id)) continue;
            var helperName = helperNamesById[link.id];
            GameObject helperGo;
            if (!ensuredHelpers.TryGetValue(helperName, out helperGo) || helperGo == null)
                continue; // pass 1 已报错

            var buttons = new List<GameObject>();
            if (link.sourceIds != null)
            {
                foreach (var sid in link.sourceIds)
                {
                    var go = ButtonLinkBakery.ResolveObject(sid, createdObjects);
                    if (go == null)
                    {
                        errors.Add("随机触发器：「" + helperName + "」源按钮不在场景中 " + sid);
                        continue;
                    }
                    if (!buttons.Contains(go)) buttons.Add(go);
                }
            }

            // 候选解析：目标名落位 + 参数 spec 编码；空信号名跳过（校验层已拦）。
            var entryGos = new List<EntryBaked>();
            if (link.entries != null)
            {
                for (int i = 0; i < link.entries.Length; i++)
                {
                    var entry = link.entries[i];
                    if (entry == null || string.IsNullOrEmpty((entry.trigger ?? "").Trim()))
                    {
                        errors.Add("随机触发器：「" + helperName + "」候选 #" + (i + 1) + " 信号名为空，跳过");
                        continue;
                    }
                    string targetName;
                    string resolveError;
                    if (!ResolveTargetRef(entry.targetRef, helperNamesById, ensuredHelpers,
                        createdObjects, out targetName, out resolveError))
                    {
                        errors.Add("随机触发器：「" + helperName + "」候选 #" + (i + 1) +
                            " " + resolveError);
                        continue;
                    }
                    entryGos.Add(new EntryBaked
                    {
                        trigger = entry.trigger.Trim(),
                        weight = entry.weight > 0f ? entry.weight : 0f,
                        targetName = targetName,
                        param = entry.param,
                    });
                }
            }
            if (entryGos.Count < 2)
            {
                // 解析失败 ≠ 删除：保留既有 helper（同轴同款防线），报错等用户修文档。
                errors.Add("随机触发器：「" + helperName + "」有效候选不足 2 个（" +
                    entryGos.Count + "），保留既有 helper 不重烘焙");
                continue;
            }

            var cooldown = Mathf.Max(0f, link.cooldownSeconds);
            var separator = string.IsNullOrEmpty(link.separator) ? ":" : link.separator;
            var inputTrigger = (link.inputTrigger ?? "").Trim();
            var buttonNames = new string[buttons.Count];
            for (int bi = 0; bi < buttons.Count; bi++)
                buttonNames[bi] = buttons[bi].name;

            AttachComponent(helperGo, inputTrigger, cooldown, separator, buttons, entryGos);
            WriteTag(helperGo, inputTrigger, cooldown, separator, buttonNames, entryGos);

            LayoutEditorLog.Log("random trigger: helper " + helperName + " 烘焙完成：" +
                entryGos.Count + " 候选｜源按钮 " + buttons.Count + "｜输入 " +
                (inputTrigger.Length == 0 ? "<任意>" : inputTrigger) +
                "｜冷却 " + cooldown.ToString("0.##", CultureInfo.InvariantCulture) + "s");
        }

        CleanupStale(usedHelpers);

        if (errors.Count > 0)
            LayoutEditorLog.LogWarning("random trigger: bake errors: " + string.Join("; ", errors.ToArray()));
        return errors.Count > 0 ? string.Join("; ", errors.ToArray()) : null;
    }

    private class EntryBaked
    {
        public string trigger;
        public float weight;
        public string targetName;
        public LayoutRandomTriggerParamDto param;
    }

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

    /// <summary>候选目标引用 → 目标 GO 名（组件/运行时按名字 SendMessage 寻址）。
    /// "" = 自身（空名）；"rt:&lt;linkId&gt;" = 其他随机触发器节点；
    /// 其余按物品引用解析（ButtonLinkBakery.ResolveObject 语义）。</summary>
    private static bool ResolveTargetRef(string targetRef,
        Dictionary<string, string> helperNamesById,
        Dictionary<string, GameObject> ensuredHelpers,
        Dictionary<string, GameObject> createdObjects,
        out string targetName, out string error)
    {
        targetName = "";
        error = null;
        if (string.IsNullOrEmpty(targetRef))
            return true; // 空 = 发给节点自身
        if (targetRef.StartsWith("rt:", StringComparison.Ordinal))
        {
            var linkId = targetRef.Substring(3);
            string helperName;
            GameObject helperGo;
            if (!helperNamesById.TryGetValue(linkId, out helperName) ||
                !ensuredHelpers.TryGetValue(helperName, out helperGo) || helperGo == null)
            {
                error = "目标随机触发器节点不在文档中 " + targetRef;
                return false;
            }
            targetName = helperGo.name;
            return true;
        }
        var go = ButtonLinkBakery.ResolveObject(targetRef, createdObjects);
        if (go == null)
        {
            error = "目标不在场景中 " + targetRef;
            return false;
        }
        targetName = go.name;
        return true;
    }

    // ---------------------------------------------------------------- component

    /// <summary>挂/重建 CustomStub.RandomTrigger（反射；真机烘焙件为 Missing
    /// Script，由 tag 自愈补挂——编辑器 Play 则本组件直接存活运行）。</summary>
    private static void AttachComponent(GameObject helper, string inputTrigger,
        float cooldown, string separator, List<GameObject> buttons, List<EntryBaked> entries)
    {
        var rtType = LayoutEditorStubIO.FindCustomStubType(helper, "RandomTrigger");
        if (rtType == null)
        {
            LayoutEditorLog.LogWarning("random trigger: 找不到 CustomStub.RandomTrigger" +
                "（WebCustomStubRuntime 未编译？）——真机将由 tag 自愈兜底，编辑器 Play 本次无效");
            return;
        }
        foreach (var old in helper.GetComponents(rtType))
            Undo.DestroyObjectImmediate(old);
        var comp = Undo.AddComponent(helper, rtType);
        SetFieldString(comp, "m_inputTrigger", inputTrigger);
        SetFieldFloat(comp, "m_cooldownSeconds", cooldown);
        SetFieldString(comp, "m_separator", separator);
        var buttonNames = new string[buttons.Count];
        for (int i = 0; i < buttons.Count; i++)
            buttonNames[i] = buttons[i].name;
        SetFieldStrings(comp, "m_buttonRootNames", buttonNames);
        SetFieldEntries(comp, "m_entries", entries);
        EditorUtility.SetDirty(comp);
    }

    /// <summary>反射构造 Entry[]（嵌套 [Serializable] 类，运行时程序集类型）。</summary>
    private static void SetFieldEntries(Component comp, string field, List<EntryBaked> entries)
    {
        var rtType = comp.GetType();
        var f = rtType.GetField(field, System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance);
        var entryType = rtType.GetNestedType("Entry");
        var paramType = rtType.GetNestedType("ParamSpec");
        if (f == null || entryType == null)
        {
            LayoutEditorLog.LogWarning("random trigger: RandomTrigger.Entry 反射缺失，候选未写入组件");
            return;
        }
        var arr = Array.CreateInstance(entryType, entries.Count);
        for (int i = 0; i < entries.Count; i++)
        {
            var inst = Activator.CreateInstance(entryType);
            SetNestedString(entryType, inst, "triggerName", entries[i].trigger);
            SetNestedFloat(entryType, inst, "weight", entries[i].weight);
            SetNestedString(entryType, inst, "targetName", entries[i].targetName);
            var p = entries[i].param;
            if (p != null && p.mode > 0 && paramType != null)
            {
                var ps = Activator.CreateInstance(paramType);
                SetNestedInt(paramType, ps, "mode", p.mode);
                SetNestedString(paramType, ps, "fixedValue", p.fixedValue ?? "");
                if (p.candidates != null && p.candidates.Length > 0)
                    SetNestedStrings(paramType, ps, "candidates", p.candidates);
                SetNestedInt(paramType, ps, "stringLength", p.stringLength);
                SetNestedString(paramType, ps, "charset", p.charset ?? "");
                SetNestedInt(paramType, ps, "digits", p.digits);
                SetNestedFloat(paramType, ps, "minValue", p.minValue);
                SetNestedFloat(paramType, ps, "maxValue", p.maxValue);
                SetNestedInt(paramType, ps, "decimals", p.decimals);
                SetNestedObject(entryType, inst, "param", ps);
            }
            arr.SetValue(inst, i);
        }
        Undo.RecordObject(comp, "Layout Editor Random Trigger");
        f.SetValue(comp, arr);
    }

    private static void SetNestedObject(Type t, object inst, string field, object value)
    {
        var f = t.GetField(field, System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance);
        if (f != null) f.SetValue(inst, value);
    }

    private static void SetNestedString(Type t, object inst, string field, string value)
    {
        var f = t.GetField(field, System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance);
        if (f != null) f.SetValue(inst, value ?? "");
    }

    private static void SetNestedStrings(Type t, object inst, string field, string[] value)
    {
        var f = t.GetField(field, System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance);
        if (f != null) f.SetValue(inst, value);
    }

    private static void SetNestedFloat(Type t, object inst, string field, float value)
    {
        var f = t.GetField(field, System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance);
        if (f != null) f.SetValue(inst, value);
    }

    private static void SetNestedInt(Type t, object inst, string field, int value)
    {
        var f = t.GetField(field, System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance);
        if (f != null) f.SetValue(inst, value);
    }

    private static void SetFieldString(Component comp, string field, string value)
    {
        var f = comp.GetType().GetField(field, System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance);
        if (f != null)
        {
            Undo.RecordObject(comp, "Layout Editor Random Trigger");
            f.SetValue(comp, value);
        }
    }

    private static void SetFieldFloat(Component comp, string field, float value)
    {
        var f = comp.GetType().GetField(field, System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance);
        if (f != null)
        {
            Undo.RecordObject(comp, "Layout Editor Random Trigger");
            f.SetValue(comp, value);
        }
    }

    private static void SetFieldStrings(Component comp, string field, string[] value)
    {
        var f = comp.GetType().GetField(field, System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance);
        if (f != null)
        {
            Undo.RecordObject(comp, "Layout Editor Random Trigger");
            f.SetValue(comp, value);
        }
    }

    // ---------------------------------------------------------------- tag

    /// <summary>写 RandomTrigger| 自愈 tag（单组件约定：helper 上无其他 tag，
    /// 可整串覆写）。浮点 invariant（解析端同约定）。</summary>
    private static void WriteTag(GameObject helper, string inputTrigger,
        float cooldown, string separator, string[] buttonNames, List<EntryBaked> entries)
    {
        try
        {
            var tagStr = BuildTag(inputTrigger, cooldown, separator, buttonNames, entries);
            var tag = helper.GetComponent<LevelEditorStub.SpecificPseudoPrefabTag>();
            if (tag == null)
                tag = Undo.AddComponent<LevelEditorStub.SpecificPseudoPrefabTag>(helper);
            else
                Undo.RecordObject(tag, "Layout Editor Random Trigger Tag");
            tag.prefabTag = tagStr;
            EditorUtility.SetDirty(tag);
        }
        catch (Exception ex)
        {
            LayoutEditorLog.LogWarning("random trigger: tag 写入失败 " + helper.name + ": " + ex.Message);
        }
    }

    /// <summary>构建 tag 字符串（WriteTag 与 RefreshTagFromComponent 共用）。</summary>
    private static string BuildTag(string inputTrigger, float cooldown,
        string separator, string[] buttonNames, List<EntryBaked> entries)
    {
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder("RandomTrigger");
        sb.Append("|I:").Append(EscRt(inputTrigger));
        sb.Append("|C:").Append(cooldown.ToString("0.###", inv));
        sb.Append("|S:").Append(EscRt(separator));
        sb.Append("|N:").Append(JoinEscRt(buttonNames ?? new string[0], ','));
        sb.Append("|E:");
        for (int i = 0; i < entries.Count; i++)
        {
            if (i > 0) sb.Append(';');
            sb.Append(EscRt(entries[i].trigger))
              .Append(',')
              .Append(entries[i].weight.ToString("0.######", inv))
              .Append(',')
              .Append(EscRt(entries[i].targetName));
            var spec = EncodeParamSpec(entries[i].param);
            if (spec.Length > 0)
                sb.Append(',').Append(spec);
        }
        return sb.ToString();
    }

    /// <summary>参数 DTO → tag spec 编码（与 EntryPoint.ParseParamSpecRt 对称）。</summary>
    internal static string EncodeParamSpec(LayoutRandomTriggerParamDto p)
    {
        if (p == null || p.mode <= 0) return "";
        var inv = CultureInfo.InvariantCulture;
        switch (p.mode)
        {
            case 1: // 固定
                return "f" + EscRt(p.fixedValue ?? "");
            case 2: // 候选
                {
                    var cands = p.candidates ?? new string[0];
                    var parts = new List<string>();
                    for (int i = 0; i < cands.Length; i++)
                        if (!string.IsNullOrEmpty(cands[i]))
                            parts.Add(EscRt(cands[i]));
                    return parts.Count > 0 ? "p" + string.Join("/", parts.ToArray()) : "";
                }
            case 3: // 随机字符串
                {
                    var len = Mathf.Clamp(p.stringLength, 1, 64);
                    var s = "s" + len.ToString(inv);
                    if (!string.IsNullOrEmpty(p.charset))
                        s += "@" + EscRt(p.charset);
                    return s;
                }
            case 4: // 随机 N 位整数
                return "d" + Mathf.Clamp(p.digits, 1, 9).ToString(inv);
            case 5: // 数值区间
                {
                    var dec = Mathf.Clamp(p.decimals, 0, 6);
                    return "r" + p.minValue.ToString("0.######", inv) + "/" +
                        p.maxValue.ToString("0.######", inv) + "/" + dec.ToString(inv);
                }
        }
        return "";
    }

    internal static string JoinEscRt(string[] parts, char sep)
    {
        var sb = new StringBuilder();
        if (parts == null) return sb.ToString();
        for (int i = 0; i < parts.Length; i++)
        {
            if (i > 0) sb.Append(sep);
            sb.Append(EscRt(parts[i]));
        }
        return sb.ToString();
    }

    /// <summary>RandomTrigger tag 字段转义（与 EntryPoint.UnescRt 对称）：
    /// %,;&gt;|/@ → %XX（比 BLRelay.EscTag 多 '/' 与 '@'——参数 spec 结构字符）。</summary>
    internal static string EscRt(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (c == '%') sb.Append("%25");
            else if (c == ',') sb.Append("%2C");
            else if (c == ';') sb.Append("%3B");
            else if (c == '>') sb.Append("%3E");
            else if (c == '|') sb.Append("%7C");
            else if (c == '/') sb.Append("%2F");
            else if (c == '@') sb.Append("%40");
            else sb.Append(c);
        }
        return sb.ToString();
    }

    // ---------------------------------------------------------------- cleanup

    /// <summary>删除文档中已不存在的 RT_* helper（文档权威；空文档=全清）。</summary>
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
            LayoutEditorLog.Log("random trigger: cleanup stale helper " + go.name);
            Undo.DestroyObjectImmediate(go);
        }
        if (root.childCount == 0 && stale.Count > 0)
            Undo.DestroyObjectImmediate(root.gameObject);
    }

    // ----------------------------------------------------------------- import

    /// <summary>从场景重建随机触发器文档数据（场景是唯一事实源）：优先组件序列化
    /// 字段（权威），组件缺失时回落 tag 解析（旧场景兜底）。目标按 GO 名反查：
    /// RT 根下的 → "rt:scene:RT:&lt;名&gt;"；其余 → "u:&lt;instanceID&gt;"。</summary>
    public static List<LayoutRandomTriggerLinkDto> ImportFromScene(Scene scene)
    {
        var result = new List<LayoutRandomTriggerLinkDto>();
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

            ImportedConfig cfg;
            if (!ReadConfig(helper.gameObject, out cfg))
                continue;
            if (cfg.entries == null || cfg.entries.Count < 1)
            {
                LayoutEditorLog.LogWarning("random trigger: helper " + name +
                    " 无有效候选，跳过回导");
                continue;
            }

            var link = new LayoutRandomTriggerLinkDto
            {
                id = ImportedMarker + name,
                sourceIds = NamesToSourceRefs(cfg.buttonNames, name),
                inputTrigger = cfg.inputTrigger ?? "",
                cooldownSeconds = cfg.cooldown,
                separator = string.IsNullOrEmpty(cfg.separator) ? ":" : cfg.separator,
                entries = cfg.entries.ToArray(),
            };
            result.Add(link);
        }
        return result;
    }

    private class ImportedConfig
    {
        public string inputTrigger;
        public float cooldown;
        public string separator;
        public string[] buttonNames;
        public List<LayoutRandomTriggerEntryDto> entries;
    }

    /// <summary>读 helper 配置：组件序列化字段（权威）→ tag 回落。</summary>
    private static bool ReadConfig(GameObject helperGo, out ImportedConfig cfg)
    {
        cfg = null;
        var rtType = LayoutEditorStubIO.FindCustomStubType(helperGo, "RandomTrigger");
        var comp = rtType != null ? helperGo.GetComponent(rtType) : null;
        if (comp != null)
        {
            cfg = new ImportedConfig();
            cfg.inputTrigger = GetString(comp, "m_inputTrigger");
            cfg.cooldown = GetFloat(comp, "m_cooldownSeconds", 0f);
            cfg.separator = GetString(comp, "m_separator");
            cfg.buttonNames = GetStrings(comp, "m_buttonRootNames");
            cfg.entries = ReadEntriesFromComponent(comp);
            return cfg.entries != null && cfg.entries.Count > 0;
        }

        // tag 回落（真机旧场景/组件丢失兜底）
        var tag = helperGo.GetComponent<LevelEditorStub.SpecificPseudoPrefabTag>();
        var tagStr = tag != null ? tag.prefabTag : null;
        if (string.IsNullOrEmpty(tagStr) || !tagStr.StartsWith("RandomTrigger|", StringComparison.Ordinal))
            return false;
        try
        {
            cfg = new ImportedConfig();
            var parts = tagStr.Split('|');
            string i = null, c = null, s = null, n = null, e = null;
            for (int k = 0; k < parts.Length; k++)
            {
                if (parts[k].StartsWith("I:", StringComparison.Ordinal)) i = parts[k].Substring(2);
                else if (parts[k].StartsWith("C:", StringComparison.Ordinal)) c = parts[k].Substring(2);
                else if (parts[k].StartsWith("S:", StringComparison.Ordinal)) s = parts[k].Substring(2);
                else if (parts[k].StartsWith("N:", StringComparison.Ordinal)) n = parts[k].Substring(2);
                else if (parts[k].StartsWith("E:", StringComparison.Ordinal)) e = parts[k].Substring(2);
            }
            cfg.inputTrigger = UnescRt(i);
            float cooldown;
            if (!float.TryParse(c, NumberStyles.Float, CultureInfo.InvariantCulture, out cooldown))
                cooldown = 0f;
            cfg.cooldown = Mathf.Max(0f, cooldown);
            cfg.separator = string.IsNullOrEmpty(s) ? ":" : UnescRt(s);
            cfg.buttonNames = SplitUnescRt(n, ',');
            var entries = new List<LayoutRandomTriggerEntryDto>();
            var segs = (e ?? "").Split(';');
            for (int k = 0; k < segs.Length; k++)
            {
                if (segs[k].Length == 0) continue;
                var f = segs[k].Split(',');
                if (f.Length < 1 || f[0].Length == 0) continue;
                var entry = new LayoutRandomTriggerEntryDto
                {
                    trigger = UnescRt(f[0]),
                    weight = 1f,
                };
                float w;
                if (f.Length > 1 && float.TryParse(f[1], NumberStyles.Float,
                    CultureInfo.InvariantCulture, out w))
                    entry.weight = w;
                if (f.Length > 2)
                    entry.targetRef = NameToTargetRef(UnescRt(f[2]));
                if (f.Length > 3 && f[3].Length > 0)
                    entry.param = ParseParamSpec(f[3]);
                if (!string.IsNullOrEmpty(entry.trigger))
                    entries.Add(entry);
            }
            cfg.entries = entries;
            return entries.Count > 0;
        }
        catch (Exception ex)
        {
            LayoutEditorLog.LogWarning("random trigger: tag 解析失败 " + helperGo.name + ": " + ex.Message);
            return false;
        }
    }

    /// <summary>反射读取组件 m_entries（嵌套 Entry[]）→ 名字层平铺列表
    ///（targetName 直读组件字段，不经 DTO 引用转换；ImportFromScene 与
    /// RefreshTagFromComponent 共用）。</summary>
    private class EntryFlat
    {
        public string trigger;
        public float weight;
        public string targetName;
        public LayoutRandomTriggerParamDto param;
    }

    private static List<EntryFlat> ReadEntryFlats(Component comp)
    {
        var result = new List<EntryFlat>();
        try
        {
            var t = comp.GetType();
            var f = t.GetField("m_entries", System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Instance);
            var arr = f != null ? f.GetValue(comp) as Array : null;
            if (arr == null) return result;
            foreach (var item in arr)
            {
                if (item == null) continue;
                var itemType = item.GetType();
                var flat = new EntryFlat
                {
                    trigger = GetNestedString(itemType, item, "triggerName"),
                    weight = GetNestedFloat(itemType, item, "weight", 1f),
                    targetName = GetNestedString(itemType, item, "targetName"),
                };
                var ps = GetNested(itemType, item, "param");
                if (ps != null)
                {
                    var psType = ps.GetType();
                    var p = new LayoutRandomTriggerParamDto
                    {
                        mode = GetNestedInt(psType, ps, "mode"),
                        fixedValue = GetNestedString(psType, ps, "fixedValue"),
                        stringLength = GetNestedInt(psType, ps, "stringLength"),
                        charset = GetNestedString(psType, ps, "charset"),
                        digits = GetNestedInt(psType, ps, "digits"),
                        minValue = GetNestedFloat(psType, ps, "minValue", 0f),
                        maxValue = GetNestedFloat(psType, ps, "maxValue", 1f),
                        decimals = GetNestedInt(psType, ps, "decimals"),
                    };
                    var cands = GetNestedStrings(psType, ps, "candidates");
                    if (cands != null) p.candidates = cands;
                    flat.param = p;
                }
                if (!string.IsNullOrEmpty(flat.trigger))
                    result.Add(flat);
            }
        }
        catch (Exception ex)
        {
            LayoutEditorLog.LogWarning("random trigger: 组件候选读取失败 " + comp.name + ": " + ex.Message);
        }
        return result;
    }

    /// <summary>组件（权威）→ tag 重算（LayoutEditorStubIO.RefreshStubTagsInActiveScene
    /// 导出期调用，捕捉 Inspector 手改组件后的 tag 漂移）。返回 tag 是否有变化。</summary>
    public static bool RefreshTagFromComponent(GameObject helperGo)
    {
        var rtType = LayoutEditorStubIO.FindCustomStubType(helperGo, "RandomTrigger");
        var comp = rtType != null ? helperGo.GetComponent(rtType) : null;
        if (comp == null) return false;
        var flats = ReadEntryFlats(comp);
        if (flats.Count == 0) return false;
        var baked = new List<EntryBaked>();
        for (int i = 0; i < flats.Count; i++)
        {
            baked.Add(new EntryBaked
            {
                trigger = flats[i].trigger,
                weight = flats[i].weight,
                targetName = flats[i].targetName,
                param = flats[i].param,
            });
        }
        var newTag = BuildTag(
            GetString(comp, "m_inputTrigger"),
            Mathf.Max(0f, GetFloat(comp, "m_cooldownSeconds", 0f)),
            GetString(comp, "m_separator"),
            GetStrings(comp, "m_buttonRootNames"),
            baked);
        var tag = helperGo.GetComponent<LevelEditorStub.SpecificPseudoPrefabTag>();
        if (tag == null || tag.prefabTag == newTag)
            return false;
        Undo.RecordObject(tag, "Layout Editor Random Trigger Tag");
        tag.prefabTag = newTag;
        EditorUtility.SetDirty(tag);
        return true;
    }

    /// <summary>反射读取组件 m_entries（嵌套 Entry[]）→ DTO 列表。</summary>
    private static List<LayoutRandomTriggerEntryDto> ReadEntriesFromComponent(Component comp)
    {
        var result = new List<LayoutRandomTriggerEntryDto>();
        var flats = ReadEntryFlats(comp);
        for (int i = 0; i < flats.Count; i++)
        {
            result.Add(new LayoutRandomTriggerEntryDto
            {
                trigger = flats[i].trigger,
                weight = flats[i].weight,
                targetRef = NameToTargetRef(flats[i].targetName),
                param = flats[i].param,
            });
        }
        return result;
    }

    /// <summary>目标 GO 名 → 文档引用：RT 根下的 RT_* → "rt:scene:RT:&lt;名&gt;"；
    /// 其余 → "u:&lt;instanceID&gt;"（找不到 → 空串=自身，告警）。</summary>
    private static string NameToTargetRef(string name)
    {
        if (string.IsNullOrEmpty(name)) return "";
        var go = GameObject.Find(name);
        if (go == null)
        {
            LayoutEditorLog.LogWarning("random trigger: 目标 " + name + " 反查失败（被删/改名），按自身处理");
            return "";
        }
        var parent = go.transform.parent;
        if (parent != null && parent.name == "RandomTrigger Logic")
            return "rt:" + ImportedMarker + name;
        return "u:" + go.GetInstanceID();
    }

    /// <summary>源按钮 GO 名 → 文档引用 id（"u:&lt;instanceID&gt;"）。</summary>
    private static string[] NamesToSourceRefs(string[] names, string helperName)
    {
        var refs = new List<string>();
        if (names == null) return refs.ToArray();
        foreach (var n in names)
        {
            if (string.IsNullOrEmpty(n)) continue;
            var go = GameObject.Find(n);
            if (go == null)
            {
                LayoutEditorLog.LogWarning("random trigger: helper " + helperName +
                    " 的源按钮 " + n + " 反查失败（被删/改名），跳过");
                continue;
            }
            var id = "u:" + go.GetInstanceID();
            if (!refs.Contains(id)) refs.Add(id);
        }
        return refs.ToArray();
    }

    // ---------------------------------------------------------------- tag 解析（import 兜底）

    /// <summary>参数 spec（已转义）→ DTO（与 EntryPoint.ParseParamSpecRt 同构）。</summary>
    private static LayoutRandomTriggerParamDto ParseParamSpec(string spec)
    {
        if (string.IsNullOrEmpty(spec)) return null;
        var mode = spec[0];
        var rest = spec.Length > 1 ? spec.Substring(1) : "";
        var p = new LayoutRandomTriggerParamDto();
        switch (mode)
        {
            case 'f':
                p.mode = 1;
                p.fixedValue = UnescRt(rest);
                return p;
            case 'p':
                {
                    p.mode = 2;
                    var cands = rest.Split('/');
                    var list = new List<string>();
                    for (int i = 0; i < cands.Length; i++)
                    {
                        var v = UnescRt(cands[i]);
                        if (v.Length > 0) list.Add(v);
                    }
                    p.candidates = list.ToArray();
                    return p;
                }
            case 's':
                {
                    p.mode = 3;
                    int at = rest.IndexOf('@');
                    if (at >= 0)
                    {
                        int len;
                        if (int.TryParse(rest.Substring(0, at), NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out len))
                            p.stringLength = len;
                        p.charset = UnescRt(rest.Substring(at + 1));
                    }
                    else
                    {
                        int len;
                        if (int.TryParse(rest, NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out len))
                            p.stringLength = len;
                    }
                    return p;
                }
            case 'd':
                {
                    p.mode = 4;
                    int d;
                    if (int.TryParse(rest, NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out d))
                        p.digits = d;
                    return p;
                }
            case 'r':
                {
                    p.mode = 5;
                    var f = rest.Split('/');
                    float min, max;
                    int dec;
                    if (f.Length > 0 && float.TryParse(f[0], NumberStyles.Float,
                        CultureInfo.InvariantCulture, out min))
                        p.minValue = min;
                    if (f.Length > 1 && float.TryParse(f[1], NumberStyles.Float,
                        CultureInfo.InvariantCulture, out max))
                        p.maxValue = max;
                    if (f.Length > 2 && int.TryParse(f[2], NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out dec))
                        p.decimals = dec;
                    return p;
                }
        }
        return null;
    }

    private static string[] SplitUnescRt(string joined, char sep)
    {
        if (string.IsNullOrEmpty(joined)) return new string[0];
        var parts = joined.Split(sep);
        for (int i = 0; i < parts.Length; i++)
            parts[i] = UnescRt(parts[i]);
        return parts;
    }

    /// <summary>与 EntryPoint.UnescRt 对称的解码（import tag 兜底用）。</summary>
    private static string UnescRt(string s)
    {
        if (string.IsNullOrEmpty(s) || s.IndexOf('%') < 0)
            return s;
        var sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '%' && i + 2 < s.Length)
            {
                var hex = s.Substring(i + 1, 2);
                if (hex == "25") { sb.Append('%'); i += 2; continue; }
                if (hex == "2C") { sb.Append(','); i += 2; continue; }
                if (hex == "3B") { sb.Append(';'); i += 2; continue; }
                if (hex == "3E") { sb.Append('>'); i += 2; continue; }
                if (hex == "7C") { sb.Append('|'); i += 2; continue; }
                if (hex == "2F") { sb.Append('/'); i += 2; continue; }
                if (hex == "40") { sb.Append('@'); i += 2; continue; }
            }
            sb.Append(s[i]);
        }
        return sb.ToString();
    }

    // ---------------------------------------------------------------- 反射读取辅助

    private static object GetNested(Type t, object inst, string field)
    {
        var f = t.GetField(field, System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance);
        return f != null ? f.GetValue(inst) : null;
    }

    private static string GetNestedString(Type t, object inst, string field)
    {
        var v = GetNested(t, inst, field) as string;
        return v ?? "";
    }

    private static string[] GetNestedStrings(Type t, object inst, string field)
    {
        return GetNested(t, inst, field) as string[];
    }

    private static float GetNestedFloat(Type t, object inst, string field, float fallback)
    {
        var f = t.GetField(field, System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance);
        if (f == null) return fallback;
        try { return (float)f.GetValue(inst); }
        catch { return fallback; }
    }

    private static int GetNestedInt(Type t, object inst, string field)
    {
        var f = t.GetField(field, System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance);
        if (f == null) return 0;
        try { return (int)f.GetValue(inst); }
        catch { return 0; }
    }

    private static string GetString(Component comp, string field)
    {
        var f = comp.GetType().GetField(field, System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance);
        var v = f != null ? f.GetValue(comp) as string : null;
        return v ?? "";
    }

    private static string[] GetStrings(Component comp, string field)
    {
        var f = comp.GetType().GetField(field, System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance);
        return f != null ? f.GetValue(comp) as string[] : new string[0];
    }

    private static float GetFloat(Component comp, string field, float fallback)
    {
        var f = comp.GetType().GetField(field, System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance);
        if (f == null) return fallback;
        try { return (float)f.GetValue(comp); }
        catch { return fallback; }
    }
}
