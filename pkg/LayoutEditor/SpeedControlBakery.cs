using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using LevelEditorStub;

/// <summary>
/// 按钮控速 / 按钮生成器批次烘焙（2026-10-14）。在 Design/Speed Logic/ 下创建
/// 隐藏逻辑 helper，挂 CustomStub.ButtonSpeedControl / ButtonSpawnerBatch（反射）
/// + SpeedCtl| / SpawnerBatch| 自愈 tag：
///  - 独立根目录（不放 Design/Button Logic——ButtonLinkBakery.CleanupStale 会删
///    该根下不在其 usedHelpers 里的全部子物体；CoaxialButtonBakery 同款原因）；
///  - 目标（步带/传送带/生成器伪根）不挂组件不改名——伪根已有
///    TravelatorReverse|/ConveyorDirectionSync| 等单用途 tag（SetCustomStubTag
///    整串覆写铁律），helper 独立承载本功能的组件与 tag；
///  - 按钮/目标引用一律【场景路径】（GameObject.Find 支持 "A/B" 形式，天然
///    唯一）——零改名副作用，不与 ButtonLink 的 _BLn 唯一命名互扰；
///  - 触发源 = 文档 switchLinks 里 targetId 直连本目标的开关（触发编排①区）；
///    按钮保留默认 SwitchReenable|0.35 自动复位（可重复按；vanilla 触发通道
///    对 stub 组件不可达，objectToTrigger 写不写均无效，保持不动）；
///  - 场景是唯一事实源：AttachExportData/ImportSwitchLinks 从组件序列化字段
///    （权威）还原文档数据，tag 为真机自愈载体。
/// tag 格式（与 EntryPoint.HealButtonSpeedControl/HealButtonSpawnerBatch 对称，
/// %,;&gt;| 做 %XX 转义；路径中的 / 不在转义集，原样保留）：
///   SpeedCtl|E:&lt;1|0&gt;|T:&lt;目标路径&gt;|N:&lt;按钮路径,..&gt;|L:&lt;s1,s2,..&gt;
///   SpawnerBatch|E:&lt;1|0&gt;|T:&lt;目标路径&gt;|N:&lt;按钮路径,..&gt;|C:&lt;count&gt;|I:&lt;interval&gt;|D:&lt;cooldown&gt;
/// </summary>
public static class SpeedControlBakery
{
    public const string RootPath = "Design/Speed Logic";
    public const string SpeedHelperPrefix = "SpdCtl_";
    public const string SpawnerHelperPrefix = "SpnB_";
    /** 最少速度档数（与 CustomStub.ButtonSpeedControl.MinLevels 一致）。 */
    public const int MinSpeedLevels = 2;
    public const int MaxBatchCount = 50;

    // ------------------------------------------------------------------ bake

    /// <summary>写回入口。fullWrite=false（items 作用域）时跳过 CleanupStale
    /// （部分文档不等于删除意图）。</summary>
    public static string Sync(Scene scene, LayoutDocumentDto doc,
        Dictionary<string, GameObject> createdObjects, bool fullWrite)
    {
        try
        {
            return SyncInner(doc, createdObjects, fullWrite);
        }
        catch (Exception e)
        {
            LayoutEditorLog.LogWarning("speedctl: bake exception: " + e);
            return "按钮控速写回异常：" + e.Message;
        }
    }

    private class SpeedTarget
    {
        public string itemId;
        public string stubKind;
        public GameObject go;
        public LayoutSpeedControlDto cfg;
    }

    private static void PurgeMisplacedSpeedControls(LayoutItemDto item)
    {
        if (item == null) return;
        if (item.stubKind != "Travelator" && item.travelator != null)
            item.travelator.speedControl = null;
        if (item.stubKind != "Conveyor" && item.conveyor != null)
            item.conveyor.speedControl = null;
    }

    private static bool GoSupportsSpeedControl(GameObject go, string stubKind)
    {
        if (go == null || string.IsNullOrEmpty(stubKind)) return false;
        if (stubKind == "Travelator")
            return go.GetComponent<PseudoPrefabTravelatorStub>() != null;
        if (stubKind == "Conveyor")
            return go.GetComponent<PseudoPrefabConveyorStub>() != null;
        return false;
    }

    private class SpawnerTarget
    {
        public string itemId;
        public GameObject go;
        public LayoutFoodSpawnerStubDto cfg;
    }

    private static string SyncInner(LayoutDocumentDto doc,
        Dictionary<string, GameObject> createdObjects, bool fullWrite)
    {
        if (doc == null || doc.items == null)
            return null;
        var errors = new List<string>();
        var usedHelpers = new HashSet<string>(StringComparer.Ordinal);

        var speedTargets = new List<SpeedTarget>();
        var spawnerTargets = new List<SpawnerTarget>();
        foreach (var item in doc.items)
        {
            if (item == null || string.IsNullOrEmpty(item.instanceId)) continue;
            PurgeMisplacedSpeedControls(item);
            if (item.stubKind == "Travelator" && item.travelator != null &&
                item.travelator.speedControl != null)
                speedTargets.Add(new SpeedTarget
                {
                    itemId = item.instanceId,
                    stubKind = item.stubKind,
                    cfg = item.travelator.speedControl
                });
            else if (item.stubKind == "Conveyor" && item.conveyor != null &&
                     item.conveyor.speedControl != null)
                speedTargets.Add(new SpeedTarget
                {
                    itemId = item.instanceId,
                    stubKind = item.stubKind,
                    cfg = item.conveyor.speedControl
                });
            else if (item.foodSpawner != null && item.foodSpawner.startByButton)
                spawnerTargets.Add(new SpawnerTarget { itemId = item.instanceId, cfg = item.foodSpawner });
        }
        if (speedTargets.Count == 0 && spawnerTargets.Count == 0)
        {
            if (fullWrite)
                CleanupStale(usedHelpers);
            return null;
        }

        // ---- 目标解析（createdObjects 优先，防 new: id 跨域重载漂移）
        var targetsByItemId = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        foreach (var st in speedTargets)
        {
            var go = ButtonLinkBakery.ResolveObject(st.itemId, createdObjects);
            if (go == null)
            {
                errors.Add("按钮控速：目标不在场景中 " + st.itemId);
                continue;
            }
            st.go = go;
            targetsByItemId[st.itemId] = go;
        }
        foreach (var sp in spawnerTargets)
        {
            var go = ButtonLinkBakery.ResolveObject(sp.itemId, createdObjects);
            if (go == null)
            {
                errors.Add("生成器批次：目标不在场景中 " + sp.itemId);
                continue;
            }
            sp.go = go;
            targetsByItemId[sp.itemId] = go;
        }

        // ---- 触发源收集：switchLinks 里 targetId 直连本目标的开关（去重）
        var buttonsByItemId = new Dictionary<string, List<GameObject>>();
        if (doc.switchLinks != null)
        {
            foreach (var link in doc.switchLinks)
            {
                if (link == null || string.IsNullOrEmpty(link.switchId) ||
                    string.IsNullOrEmpty(link.targetId)) continue;
                List<GameObject> list;
                if (!buttonsByItemId.TryGetValue(link.targetId, out list))
                {
                    list = new List<GameObject>();
                    buttonsByItemId[link.targetId] = list;
                }
                var btn = ButtonLinkBakery.ResolveObject(link.switchId, createdObjects);
                if (btn == null)
                {
                    LayoutEditorLog.LogWarning("speedctl: 触发源开关不在场景中 " + link.switchId + "，跳过");
                    continue;
                }
                if (!list.Contains(btn)) list.Add(btn);
            }
        }

        // ---- 烘焙：速度控制 helper
        foreach (var st in speedTargets)
        {
            if (st.go == null) continue;
            if (!GoSupportsSpeedControl(st.go, st.stubKind))
            {
                LayoutEditorLog.Log("speedctl: 跳过非步带/传送带目标 " + st.go.name +
                    "（stubKind=" + (st.stubKind ?? "?") + "，已忽略误挂的按钮控速）");
                continue;
            }
            var cfg = st.cfg ?? new LayoutSpeedControlDto();
            var levels = cfg.levels;
            if (levels == null || levels.Length < MinSpeedLevels)
            {
                errors.Add("按钮控速：目标 " + st.go.name + " 速度档位不足 " + MinSpeedLevels +
                    " 个，已跳过");
                continue;
            }
            List<GameObject> buttons;
            buttonsByItemId.TryGetValue(st.itemId, out buttons);
            if (buttons == null || buttons.Count == 0)
            {
                errors.Add("按钮控速：目标 " + st.go.name + " 未绑定任何开关（触发编排①区添加），已跳过");
                continue;
            }

            var helperName = SpeedHelperPrefix + ButtonLinkBakery.Hash8(st.itemId);
            var helper = EnsureHelper(helperName);
            if (helper == null)
            {
                errors.Add("按钮控速：无法创建 " + RootPath + "/" + helperName);
                continue;
            }
            usedHelpers.Add(helperName);

            AttachSpeedComponent(helper.gameObject, ScenePathOf(st.go),
                PathArrayOf(buttons), cfg.enabled, levels);
            WriteSpeedTag(helper.gameObject, ScenePathOf(st.go),
                PathArrayOf(buttons), cfg.enabled, levels);

            LayoutEditorLog.Log("speedctl: helper " + helperName + " 烘焙完成：目标 " +
                st.go.name + "｜档位 " + levels.Length + "｜按钮 " + buttons.Count + " 个" +
                (cfg.enabled ? "" : "（已停用）"));
        }

        // ---- 烘焙：生成器批次 helper
        foreach (var sp in spawnerTargets)
        {
            if (sp.go == null) continue;
            var cfg = sp.cfg ?? new LayoutFoodSpawnerStubDto();
            List<GameObject> buttons;
            buttonsByItemId.TryGetValue(sp.itemId, out buttons);
            if (buttons == null || buttons.Count == 0)
            {
                errors.Add("生成器批次：目标 " + sp.go.name + " 未绑定任何开关（触发编排①区添加），已跳过");
                continue;
            }
            int count = Mathf.Clamp(cfg.batchCount <= 0 ? 1 : cfg.batchCount, 1, MaxBatchCount);
            float interval = Mathf.Max(0.1f, cfg.triggerTime > 0f ? cfg.triggerTime : 5f);
            float cooldown = Mathf.Max(0f, cfg.buttonCooldown);

            var helperName = SpawnerHelperPrefix + ButtonLinkBakery.Hash8(sp.itemId);
            var helper = EnsureHelper(helperName);
            if (helper == null)
            {
                errors.Add("生成器批次：无法创建 " + RootPath + "/" + helperName);
                continue;
            }
            usedHelpers.Add(helperName);

            AttachSpawnerComponent(helper.gameObject, ScenePathOf(sp.go),
                PathArrayOf(buttons), count, interval, cooldown);
            WriteSpawnerTag(helper.gameObject, ScenePathOf(sp.go),
                PathArrayOf(buttons), count, interval, cooldown);

            LayoutEditorLog.Log("speedctl: helper " + helperName + " 烘焙完成：目标 " +
                sp.go.name + "｜每批 " + count + "｜间隔 " + interval.ToString("0.##") +
                "s｜冷却 " + cooldown.ToString("0.##") + "s｜按钮 " + buttons.Count + " 个");
        }

        if (fullWrite)
            CleanupStale(usedHelpers);

        if (errors.Count > 0)
            LayoutEditorLog.LogWarning("speedctl: bake errors: " + string.Join("; ", errors.ToArray()));
        return errors.Count > 0 ? string.Join("; ", errors.ToArray()) : null;
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

    /// <summary>场景路径（场景根起算，"A/B" 形式；GameObject.Find 可解析）。</summary>
    internal static string ScenePathOf(GameObject go)
    {
        if (go == null) return "";
        var sb = new StringBuilder(go.name);
        var t = go.transform.parent;
        while (t != null)
        {
            sb.Insert(0, t.name + "/");
            t = t.parent;
        }
        return sb.ToString();
    }

    private static string[] PathArrayOf(List<GameObject> list)
    {
        var arr = new string[list.Count];
        for (int i = 0; i < list.Count; i++)
            arr[i] = ScenePathOf(list[i]);
        return arr;
    }

    /// <summary>挂/重建 ButtonSpeedControl（反射；真机烘焙件为 Missing Script，
    /// 由 tag 自愈补挂——编辑器 Play 则本组件直接存活运行）。</summary>
    private static void AttachSpeedComponent(GameObject helper, string targetPath,
        string[] buttonPaths, bool enabled, float[] levels)
    {
        var type = LayoutEditorStubIO.FindCustomStubType(helper, "ButtonSpeedControl");
        if (type == null)
        {
            LayoutEditorLog.LogWarning("speedctl: 找不到 CustomStub.ButtonSpeedControl" +
                "（WebCustomStubRuntime 未编译？）——真机将由 tag 自愈兜底，编辑器 Play 本次无效");
            return;
        }
        foreach (var old in helper.GetComponents(type))
            Undo.DestroyObjectImmediate(old);
        var comp = Undo.AddComponent(helper, type);
        SetField(comp, "m_enabled", enabled);
        SetField(comp, "m_targetPath", targetPath);
        SetField(comp, "m_buttonPaths", buttonPaths);
        SetField(comp, "m_speeds", levels);
        EditorUtility.SetDirty(comp);
    }

    private static void AttachSpawnerComponent(GameObject helper, string targetPath,
        string[] buttonPaths, int count, float interval, float cooldown)
    {
        var type = LayoutEditorStubIO.FindCustomStubType(helper, "ButtonSpawnerBatch");
        if (type == null)
        {
            LayoutEditorLog.LogWarning("speedctl: 找不到 CustomStub.ButtonSpawnerBatch" +
                "（WebCustomStubRuntime 未编译？）——真机将由 tag 自愈兜底，编辑器 Play 本次无效");
            return;
        }
        foreach (var old in helper.GetComponents(type))
            Undo.DestroyObjectImmediate(old);
        var comp = Undo.AddComponent(helper, type);
        SetField(comp, "m_enabled", true);
        SetField(comp, "m_targetPath", targetPath);
        SetField(comp, "m_buttonPaths", buttonPaths);
        SetField(comp, "m_count", count);
        SetField(comp, "m_intervalSeconds", interval);
        SetField(comp, "m_cooldownSeconds", cooldown);
        EditorUtility.SetDirty(comp);
    }

    private static void SetField(Component comp, string field, object value)
    {
        var f = comp.GetType().GetField(field,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (f != null)
        {
            Undo.RecordObject(comp, "Layout Editor Speed Control");
            f.SetValue(comp, value);
        }
    }

    private static object GetField(Component comp, string field)
    {
        if (comp == null) return null;
        var f = comp.GetType().GetField(field,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        return f != null ? f.GetValue(comp) : null;
    }

    /// <summary>写 SpeedCtl| tag（helper 单用途，可整串覆写；浮点 invariant）。</summary>
    private static void WriteSpeedTag(GameObject helper, string targetPath,
        string[] buttonPaths, bool enabled, float[] levels)
    {
        try
        {
            var sb = new StringBuilder("SpeedCtl");
            sb.Append("|E:").Append(enabled ? "1" : "0");
            sb.Append("|T:").Append(ButtonLinkBakery.EscTag(targetPath));
            sb.Append("|N:").Append(ButtonLinkBakery.JoinEsc(buttonPaths, ','));
            sb.Append("|L:");
            for (int i = 0; i < levels.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(levels[i].ToString("0.###",
                    System.Globalization.CultureInfo.InvariantCulture));
            }
            WriteTag(helper, sb.ToString());
        }
        catch (Exception ex)
        {
            LayoutEditorLog.LogWarning("speedctl: SpeedCtl tag 写入失败 " + helper.name + ": " + ex.Message);
        }
    }

    private static void WriteSpawnerTag(GameObject helper, string targetPath,
        string[] buttonPaths, int count, float interval, float cooldown)
    {
        try
        {
            var sb = new StringBuilder("SpawnerBatch");
            sb.Append("|E:1");
            sb.Append("|T:").Append(ButtonLinkBakery.EscTag(targetPath));
            sb.Append("|N:").Append(ButtonLinkBakery.JoinEsc(buttonPaths, ','));
            sb.Append("|C:").Append(count);
            sb.Append("|I:").Append(interval.ToString("0.###",
                System.Globalization.CultureInfo.InvariantCulture));
            sb.Append("|D:").Append(cooldown.ToString("0.###",
                System.Globalization.CultureInfo.InvariantCulture));
            WriteTag(helper, sb.ToString());
        }
        catch (Exception ex)
        {
            LayoutEditorLog.LogWarning("speedctl: SpawnerBatch tag 写入失败 " + helper.name + ": " + ex.Message);
        }
    }

    private static void WriteTag(GameObject helper, string tagValue)
    {
        var tag = helper.GetComponent<LevelEditorStub.SpecificPseudoPrefabTag>();
        if (tag == null)
            tag = Undo.AddComponent<LevelEditorStub.SpecificPseudoPrefabTag>(helper);
        else
            Undo.RecordObject(tag, "Layout Editor Speed Control Tag");
        tag.prefabTag = tagValue;
        EditorUtility.SetDirty(tag);
    }

    // ---------------------------------------------------------------- cleanup

    /// <summary>删除文档中已不存在的 helper（仅全量写回；空文档=全清）。</summary>
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
            LayoutEditorLog.Log("speedctl: cleanup stale helper " + go.name);
            Undo.DestroyObjectImmediate(go);
        }
        if (root.childCount == 0 && stale.Count > 0)
            Undo.DestroyObjectImmediate(root.gameObject);
    }

    // ----------------------------------------------------------------- import

    /// <summary>回导：按场景 helper 反查，把 speedControl / spawner 批次配置填回
    /// item DTO（SceneLayoutExporter 在 items 收集后调用；无 helper = null/默认）。</summary>
    public static void AttachExportData(List<LayoutItemDto> items)
    {
        if (items == null) return;
        var speedHelpers = new List<HelperInfo>(CollectHelpers(SpeedHelperPrefix).Keys);
        var spawnerHelpers = new List<HelperInfo>(CollectHelpers(SpawnerHelperPrefix).Keys);

        foreach (var item in items)
        {
            if (item == null || string.IsNullOrEmpty(item.instanceId) ||
                !item.instanceId.StartsWith("u:", StringComparison.Ordinal)) continue;
            int id;
            if (!int.TryParse(item.instanceId.Substring(2), out id)) continue;
            var go = EditorUtility.InstanceIDToObject(id) as GameObject;
            if (go == null) continue;
            var path = ScenePathOf(go);

            PurgeMisplacedSpeedControls(item);
            if (item.stubKind == "Travelator" && item.travelator != null)
            {
                LayoutSpeedControlDto cfg = null;
                for (int i = 0; i < speedHelpers.Count; i++)
                {
                    if (speedHelpers[i].targetPath == path)
                    {
                        var lv = speedHelpers[i].levels;
                        cfg = new LayoutSpeedControlDto
                        {
                            enabled = speedHelpers[i].enabled,
                            levels = lv != null ? (float[])lv.Clone() : null
                        };
                        break;
                    }
                }
                item.travelator.speedControl = cfg;
            }
            else if (item.stubKind == "Conveyor" && item.conveyor != null)
            {
                LayoutSpeedControlDto cfg = null;
                for (int i = 0; i < speedHelpers.Count; i++)
                {
                    if (speedHelpers[i].targetPath == path)
                    {
                        var lv = speedHelpers[i].levels;
                        cfg = new LayoutSpeedControlDto
                        {
                            enabled = speedHelpers[i].enabled,
                            levels = lv != null ? (float[])lv.Clone() : null
                        };
                        break;
                    }
                }
                item.conveyor.speedControl = cfg;
            }
            if (item.foodSpawner != null)
            {
                HelperInfo found = null;
                for (int i = 0; i < spawnerHelpers.Count; i++)
                {
                    if (spawnerHelpers[i].targetPath == path)
                    {
                        found = spawnerHelpers[i];
                        break;
                    }
                }
                if (found != null)
                {
                    item.foodSpawner.startByButton = true;
                    item.foodSpawner.batchCount = found.count > 0 ? found.count : 1;
                    item.foodSpawner.buttonCooldown = found.cooldown;
                    if (found.interval > 0f) item.foodSpawner.triggerTime = found.interval;
                }
                else
                {
                    item.foodSpawner.startByButton = false;
                }
            }
        }
    }

    /// <summary>回导：helper → switchLinks（switchId/targetId = u:instanceID；
    /// trigger 留空——stub 轮询通道无触发名语义）。</summary>
    public static List<LayoutSwitchLinkDto> ImportSwitchLinks(Scene scene)
    {
        var result = new List<LayoutSwitchLinkDto>();
        if (!scene.IsValid()) return result;

        var all = new List<HelperInfo>(CollectHelpers(SpeedHelperPrefix).Keys);
        all.AddRange(CollectHelpers(SpawnerHelperPrefix).Keys);

        foreach (var info in all)
        {
            if (info == null || string.IsNullOrEmpty(info.targetPath)) continue;
            var target = GameObject.Find(info.targetPath);
            if (target == null)
            {
                LayoutEditorLog.LogWarning("speedctl: 回导目标路径未命中 " + info.targetPath);
                continue;
            }
            foreach (var btnPath in info.buttonPaths ?? new string[0])
            {
                var btn = GameObject.Find(btnPath);
                if (btn == null)
                {
                    LayoutEditorLog.LogWarning("speedctl: 回导按钮路径未命中 " + btnPath);
                    continue;
                }
                result.Add(new LayoutSwitchLinkDto
                {
                    switchId = "u:" + btn.GetInstanceID(),
                    targetId = "u:" + target.GetInstanceID(),
                    trigger = ""
                });
            }
        }
        return result;
    }

    private class HelperInfo
    {
        public string targetPath;
        public string[] buttonPaths;
        public bool enabled;
        public float[] levels;
        public int count;
        public float interval;
        public float cooldown;
    }

    /// <summary>收集根下指定前缀 helper 的配置（组件权威 → tag 兜底）。</summary>
    private static Dictionary<HelperInfo, string> CollectHelpers(string prefix)
    {
        var result = new Dictionary<HelperInfo, string>();
        var root = LayoutEditorHierarchy.FindByPath(RootPath);
        if (root == null) return result;
        for (int i = 0; i < root.childCount; i++)
        {
            var helper = root.GetChild(i);
            if (!helper.name.StartsWith(prefix, StringComparison.Ordinal)) continue;
            var info = ReadHelper(helper.gameObject, prefix);
            if (info != null && !string.IsNullOrEmpty(info.targetPath))
                result[info] = info.targetPath;
        }
        return result;
    }

    private static HelperInfo ReadHelper(GameObject helperGo, string prefix)
    {
        var info = new HelperInfo();
        var typeName = prefix == SpeedHelperPrefix ? "ButtonSpeedControl" : "ButtonSpawnerBatch";
        var type = LayoutEditorStubIO.FindCustomStubType(helperGo, typeName);
        var comp = type != null ? helperGo.GetComponent(type) : null;
        if (comp != null)
        {
            info.targetPath = GetField(comp, "m_targetPath") as string;
            info.buttonPaths = GetField(comp, "m_buttonPaths") as string[];
            info.enabled = GetField(comp, "m_enabled") is bool && (bool)GetField(comp, "m_enabled");
            info.levels = GetField(comp, "m_speeds") as float[];
            var count = GetField(comp, "m_count");
            if (count is int) info.count = (int)count;
            var iv = GetField(comp, "m_intervalSeconds");
            if (iv is float) info.interval = (float)iv;
            var cd = GetField(comp, "m_cooldownSeconds");
            if (cd is float) info.cooldown = (float)cd;
            return info;
        }
        // tag 兜底（组件缺失/编辑器未编译）。注意 prefabTag 以 tag 前缀
        // （SpeedCtl|/SpawnerBatch|）开头，不是 helper 名前缀（SpdCtl_/SpnB_）。
        var tag = helperGo.GetComponent<SpecificPseudoPrefabTag>();
        var tagPrefix = prefix == SpeedHelperPrefix ? "SpeedCtl|" : "SpawnerBatch|";
        if (tag == null || string.IsNullOrEmpty(tag.prefabTag) ||
            !tag.prefabTag.StartsWith(tagPrefix, StringComparison.Ordinal)) return null;
        ParseTag(tag.prefabTag, prefix, info);
        return info;
    }

    private static void ParseTag(string prefabTag, string prefix, HelperInfo info)
    {
        // 与 EntryPoint.HealButtonSpeedControl/HealButtonSpawnerBatch 对称（未转义
        // 回原值的宽松解析——回导仅兜底用途，权威在组件）。
        var parts = prefabTag.Split('|');
        for (int i = 1; i < parts.Length; i++)
        {
            var seg = parts[i];
            if (seg.StartsWith("E:", StringComparison.Ordinal))
                info.enabled = seg.Substring(2) != "0";
            else if (seg.StartsWith("T:", StringComparison.Ordinal))
                info.targetPath = Unesc(seg.Substring(2));
            else if (seg.StartsWith("N:", StringComparison.Ordinal))
            {
                var names = seg.Substring(2).Split(',');
                var list = new List<string>();
                foreach (var n in names)
                {
                    var v = Unesc(n);
                    if (v.Length > 0) list.Add(v);
                }
                info.buttonPaths = list.ToArray();
            }
            else if (seg.StartsWith("L:", StringComparison.Ordinal))
            {
                var segs = seg.Substring(2).Split(',');
                var list = new List<float>();
                foreach (var s in segs)
                {
                    float v;
                    if (float.TryParse(s.Trim(),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out v))
                        list.Add(v);
                }
                info.levels = list.ToArray();
            }
            else if (seg.StartsWith("C:", StringComparison.Ordinal))
            {
                int v;
                if (int.TryParse(seg.Substring(2), out v)) info.count = v;
            }
            else if (seg.StartsWith("I:", StringComparison.Ordinal))
            {
                float v;
                if (float.TryParse(seg.Substring(2),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out v))
                    info.interval = v;
            }
            else if (seg.StartsWith("D:", StringComparison.Ordinal))
            {
                float v;
                if (float.TryParse(seg.Substring(2),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out v))
                    info.cooldown = v;
            }
        }
    }

    private static string Unesc(string s)
    {
        if (string.IsNullOrEmpty(s) || s.IndexOf('%') < 0) return s;
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
            }
            sb.Append(s[i]);
        }
        return sb.ToString();
    }
}
