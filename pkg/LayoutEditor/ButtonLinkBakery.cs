using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.SceneManagement;
using LevelEditorStub;

/// <summary>
/// 按钮/压力开关 ↔ 动画组联动烘焙。在 Design/Button Logic/Btn(Logic|Pair)_*&lt;key&gt; 下创建
/// 隐藏逻辑物体（Animator + 生成的 controller + TriggerOnAnimator 中继），全部用宿主原语实现：
///  - 顺序触发：状态环 Ready_i --Advance--> Run_i（进入即用 SendTriggerToObject 启动组 i）
///    --Done_i--> Ready_{i+1}；支持 loop（A-B-C-A）和 pingpong（A-B-C-B-A）；
///  - 运行期锁定（lockUntilFinished）：Run_i 无 Advance 出口且挂 ClearTriggerDuringState，
///    组运行期间的按压被忽略且不会锁存（"动画组完成后才可再按"）；
///  - 互锁对（pairId 一对一，每方至多 2 组）：AReady→ARun→(两组均完成的 AND 门)→BReady→BRun→…→
///    AReady 状态环；ARun 进入时同时启动 A 方各组，全部完成后对方才可按，反之亦然。
///    （注意与「共轭按钮」sharedSourceIds 区分：共轭 = 多按钮共用一条 link、同按同解锁；
///     互锁 = 两条 link 共享 pairId、一对一轮换抬起。）
/// 场景是唯一事实源：ImportFromScene 从 helper 接线重建文档数据（按钮 stub 的
/// animatorToTrigger / triggerOnAnimator(Enter) 指向 helper 的 Animator）。
/// </summary>
public static class ButtonLinkBakery
{
    public const string ButtonLogicRootPath = "Design/Button Logic";
    public const string HelperSeqPrefix = "BtnLogic_";
    public const string HelperSeqNoLockPrefix = "BtnLogicNL_";
    public const string HelperPairPrefix = "BtnPair_";
    /// <summary>无操作 trigger：压力开关的 Exit 事件必须指向有效参数，否则宿主
    ///  PseudoPrefabPressureSwitch.Setup 会装一个空名 TriggerOnAnimator。参数存在但无过渡消费。</summary>
    public const string NoopTrigger = "BLNoop";
    /// <summary>互锁模式下单个按钮最多绑定的动画组数。</summary>
    public const int PairGroupLimit = 2;

    private const string ImportedSeqMarker = "scene:BtnLogic:";
    private const string ImportedPairMarker = "scene:BtnPair:";

    /// <summary>该路径是否为按钮联动逻辑控制器资产（AnimGroupBakery.CleanupStale 据此跳过；
    ///  _BtnEvt 为按钮事件组资产，见 ButtonEventBakery）。</summary>
    public static bool IsButtonLogicAsset(string normalizedAssetPath)
    {
        if (string.IsNullOrEmpty(normalizedAssetPath)) return false;
        return normalizedAssetPath.Contains("_BtnLogic") || normalizedAssetPath.Contains("_BtnPair")
            || normalizedAssetPath.Contains("_BtnEvt");
    }

    // ------------------------------------------------------------------ naming

    /// <summary>稳定短哈希（helper 命名；CoaxialButtonBakery 共享）。</summary>
    internal static string Hash8(string s)
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

    /// <summary>顺序联动的 helper 名（对回导 link 幂等：已含前缀则原样返回）。</summary>
    private static string HelperNameForSeq(LayoutButtonLinkDto link)
    {
        var id = link != null ? link.id ?? "" : "";
        if (id.StartsWith(ImportedSeqMarker, StringComparison.Ordinal))
            return id.Substring(ImportedSeqMarker.Length);
        var key = Hash8(id);
        return (link != null && !link.lockUntilFinished ? HelperSeqNoLockPrefix : HelperSeqPrefix) + key;
    }

    private static string HelperNameForPair(string pairId)
    {
        pairId = pairId ?? "";
        if (pairId.StartsWith(ImportedPairMarker, StringComparison.Ordinal))
            return pairId.Substring(ImportedPairMarker.Length);
        return HelperPairPrefix + Hash8(pairId);
    }

    private static string AdvanceTrigger(string helperName) { return "BLAdv_" + helperName; }
    private static string PressTrigger(string helperName, string side) { return "BLP_" + helperName + "_" + side; }
    private static string GoTrigger(string helperName, string side, int i)
    {
        return string.IsNullOrEmpty(side)
            ? "BLGo_" + helperName + "_" + i
            : "BLGo_" + helperName + "_" + side + i;
    }
    private static string DoneTrigger(string helperName, string side, int i)
    {
        return string.IsNullOrEmpty(side)
            ? "BLDone_" + helperName + "_" + i
            : "BLD_" + helperName + "_" + side + i;
    }

    // ------------------------------------------------------------------ resolve

    /// <summary>文档 id → 场景物体（"u:&lt;instanceID&gt;" 既有 / "new:..." 本轮新建；
    /// CoaxialButtonBakery 共享）。createdObjects 优先（applier 已把按 u:/hierarchyPath
    /// 兜底匹配出的实际物体全部登记在内——u: id 跨域重载/重开场景会漂移，直查
    /// InstanceIDToObject 会把存活物体误判成 null；与 StubIO.ResolveRefObject 同款
    /// 优先级，2026-09-28 同轴按钮「成员不在场景中」误报根因）。</summary>
    internal static GameObject ResolveObject(string id, Dictionary<string, GameObject> createdObjects)
    {
        if (string.IsNullOrEmpty(id)) return null;
        GameObject go = null;
        if (createdObjects != null)
            createdObjects.TryGetValue(id, out go);
        if (go == null && id.StartsWith("u:", StringComparison.Ordinal))
        {
            int iid;
            if (int.TryParse(id.Substring(2), out iid))
                go = EditorUtility.InstanceIDToObject(iid) as GameObject;
        }
        return go;
    }

    private static Transform ResolveGroupRoot(AnimGroupDto group)
    {
        if (group == null) return null;
        if (!string.IsNullOrEmpty(group.groupHierarchyPath))
        {
            var t = LayoutEditorHierarchy.FindByPath(group.groupHierarchyPath);
            if (t != null) return t;
        }
        var name = (string.IsNullOrEmpty(group.displayName) ? "MoveGroup" : group.displayName)
            .Replace('/', '_').Replace('\\', '_');
        return LayoutEditorHierarchy.FindByPath("Design/Animated Objects/" + name);
    }

    private static Dictionary<string, AnimGroupDto> GroupMapByName(LayoutDocumentDto doc)
    {
        var byName = new Dictionary<string, AnimGroupDto>(StringComparer.Ordinal);
        foreach (var g in doc.AnimControls != null ? doc.AnimControls.groups ?? new AnimGroupDto[0] : new AnimGroupDto[0])
        {
            if (g == null || string.IsNullOrEmpty(g.displayName)) continue;
            if (!byName.ContainsKey(g.displayName))
                byName[g.displayName] = g;
            else
                LayoutEditorLog.LogWarning("button link: 动画组名重复 \"" + g.displayName + "\"，联动只绑定第一个");
        }
        return byName;
    }

    // ------------------------------------------------------------ phase 1: groups

    /// <summary>在 AnimGroupBakery.Sync 之前调用：为被联动绑定的动画组覆写
    ///  startTrigger/endTrigger（确定性命名），并清零 startDelay（绑定后由按钮控制，
    ///  保留延迟启动会导致开局自动触发一次）。</summary>
    public static void PrepareGroups(LayoutDocumentDto doc)
    {
        if (doc == null || doc.buttonLinks == null || doc.buttonLinks.links == null) return;
        if (doc.AnimControls == null || doc.AnimControls.groups == null) return;

        var byName = GroupMapByName(doc);
        var pairSides = new Dictionary<string, string>(StringComparer.Ordinal);
        var boundGroups = new HashSet<string>(StringComparer.Ordinal);
        // 同 SyncInner：组名 → 绑定者，互锁对复用同组（共享环）时静默跳过。
        var boundGroupOwner = new Dictionary<string, string>(StringComparer.Ordinal);

        // 预统计每个 pairId 的 link 数：只有一方的互锁降级为普通顺序联动
        // （与 SyncInner 的降级路径命名保持一致，否则组的触发名与 helper 不匹配）。
        var pairCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var l in doc.buttonLinks.links)
        {
            if (l == null || string.IsNullOrEmpty(l.pairId)) continue;
            int c;
            pairCounts.TryGetValue(l.pairId, out c);
            pairCounts[l.pairId] = c + 1;
        }

        foreach (var link in doc.buttonLinks.links)
        {
            if (link == null || string.IsNullOrEmpty(link.sourceId)) continue;
            if (link.groupNames == null || link.groupNames.Length == 0) continue;

            bool isPair = !string.IsNullOrEmpty(link.pairId) && pairCounts[link.pairId] >= 2;
            string side = "";
            string trigBase;
            if (isPair)
            {
                if (!pairSides.ContainsKey(link.pairId))
                {
                    pairSides[link.pairId] = "A";
                    side = "A";
                }
                else
                {
                    side = "B";
                }
                trigBase = HelperNameForPair(link.pairId);
            }
            else
            {
                trigBase = HelperNameForSeq(link);
            }

            int count = link.groupNames.Length;
            if (isPair && count > PairGroupLimit)
            {
                LayoutEditorLog.LogWarning("button link: 互锁模式每个按钮最多 " + PairGroupLimit +
                    " 组，「" + link.sourceId + "」多余的已忽略");
                count = PairGroupLimit;
            }

            for (int i = 0; i < count; i++)
            {
                var gname = link.groupNames[i];
                if (string.IsNullOrEmpty(gname)) continue;
                AnimGroupDto g;
                if (!byName.TryGetValue(gname, out g))
                {
                    LayoutEditorLog.LogWarning("button link: 动画组「" + gname + "」不存在，跳过绑定");
                    continue;
                }
                // The link is authoritative for imported/legacy groups too.
                // Persisting this marker keeps them visible in the web button
                // group picker after the next scene round-trip.
                g.triggerMode = "button";
                if (!boundGroups.Add(gname))
                {
                    string owner;
                    if (isPair && boundGroupOwner.TryGetValue(gname, out owner) && owner == link.pairId)
                        continue; // 共享环：同对另一侧复用本侧触发名（A 侧先到先写）
                    LayoutEditorLog.LogWarning("button link: 动画组「" + gname + "」已被其他联动绑定，跳过后续绑定");
                    continue;
                }
                boundGroupOwner[gname] = isPair ? link.pairId : link.sourceId;
                g.startTrigger = GoTrigger(trigBase, side, i);
                g.endTrigger = DoneTrigger(trigBase, side, i);
                // 完成回报必须等 clip 真正播完：waitForFinished=false 时队列不等
                // AnimationFinished 就 AdvanceQueue，endTrigger（BLDone）会立即返回
                // helper ——「动画组完成后才可再按」的运行期锁定形同虚设。
                // 置 true 后 clip 会在烘焙期内嵌完成事件（AnimGroupBakery 依据
                // 本字段决定是否嵌入），链路才是真正的「播完才解锁」。
                g.waitForFinished = true;
                if (g.startDelay > 0f)
                {
                    LayoutEditorLog.LogWarning("button link: 动画组「" + gname +
                        "」的启动延迟已清零（绑定后由按钮控制）");
                    g.startDelay = 0f;
                }
                if (g.loop)
                    LayoutEditorLog.LogWarning("button link: 动画组「" + gname +
                        "」循环执行不会结束，锁定时按钮将无法再按");
            }
        }
    }

    // ------------------------------------------------------------- phase 2: bake

    public static string Sync(Scene scene, LayoutDocumentDto doc, Dictionary<string, GameObject> createdObjects)
    {
        try
        {
            return SyncInner(scene, doc, createdObjects);
        }
        catch (Exception e)
        {
            LayoutEditorLog.LogWarning("button link: bake exception: " + e);
            return "按钮联动写回异常：" + e.Message;
        }
    }

    private static string SyncInner(Scene scene, LayoutDocumentDto doc, Dictionary<string, GameObject> createdObjects)
    {
        var sceneName = Path.GetFileNameWithoutExtension(scene.path);
        var animDir = AnimGroupBakery.GetAnimationsFolder(scene.path);
        var errors = new List<string>();
        var usedHelpers = new HashSet<string>(StringComparer.Ordinal);
        var usedAssets = new HashSet<string>(StringComparer.Ordinal);

        var links = doc != null && doc.buttonLinks != null && doc.buttonLinks.links != null
            ? doc.buttonLinks.links
            : new LayoutButtonLinkDto[0];

        var byName = GroupMapByName(doc);
        var boundGroups = new HashSet<string>(StringComparer.Ordinal);
        // 组名 → 绑定者（互锁 pairId / 顺序 sourceId）：互锁对双方绑【同一个组】
        // = 共享环模式（如「传送带阵 ×4 + 互锁双按钮」），第二条链路复用 A 侧
        // 触发名、不告警不覆盖。
        var boundGroupOwner = new Dictionary<string, string>(StringComparer.Ordinal);
        var pairSeen = new Dictionary<string, LayoutButtonLinkDto>(StringComparer.Ordinal);
        var pairBaked = new HashSet<string>(StringComparer.Ordinal);

        if (links.Length > 0)
            AnimGroupBakery.EnsureFolder(animDir);

        // 联动源预处理：唯一命名（BLRelay 身份通道 N:/X: 与运行期 GameObject.Find
        // 都以 GO 名为键）+ 停用 SwitchReenable 自动复位（互锁/共轭按钮生命周期由
        // relay 统一投递，0.35s 自动复位会把互锁按下侧提前拉回绿）。
        PrepareLinkSources(links, createdObjects);

        foreach (var link in links)
        {
            if (link == null || string.IsNullOrEmpty(link.sourceId)) continue;
            if ((link.groupNames == null || link.groupNames.Length == 0) && !HasButtonRelation(link)) continue;

            if (!string.IsNullOrEmpty(link.pairId))
            {
                if (pairBaked.Contains(link.pairId)) continue;
                LayoutButtonLinkDto first;
                if (!pairSeen.TryGetValue(link.pairId, out first))
                {
                    pairSeen[link.pairId] = link;
                    continue; // 等配对另一方出现后再整体烘焙
                }
                pairBaked.Add(link.pairId);
                var err = BakePair(scene, first, link, byName, boundGroups, boundGroupOwner,
                    animDir, sceneName, createdObjects, usedHelpers, usedAssets);
                if (!string.IsNullOrEmpty(err)) errors.Add(err);
                continue;
            }

            var seqErr = BakeSequence(scene, link, byName, boundGroups, animDir, sceneName,
                createdObjects, usedHelpers, usedAssets);
            if (!string.IsNullOrEmpty(seqErr)) errors.Add(seqErr);
        }

        // 孤单一方的配对（partner 缺失）按普通顺序联动降级烘焙，避免配置悬空。
        foreach (var kv in pairSeen)
        {
            var link = kv.Value;
            bool baked = false;
            foreach (var l in links)
            {
                if (l != null && l != link && l.pairId == link.pairId) { baked = true; break; }
            }
            if (baked) continue;
            LayoutEditorLog.LogWarning("button link: 互锁对「" + link.pairId + "」只有一方，降级为普通顺序联动");
            link.pairId = null;
            var err = BakeSequence(scene, link, byName, boundGroups, animDir, sceneName,
                createdObjects, usedHelpers, usedAssets);
            if (!string.IsNullOrEmpty(err)) errors.Add(err);
        }

        CleanupStale(animDir, sceneName, usedHelpers, usedAssets);
        AssetDatabase.SaveAssets();

        if (errors.Count > 0)
            LayoutEditorLog.LogWarning("button link: bake errors: " + string.Join("; ", errors.ToArray()));
        return errors.Count > 0 ? string.Join("; ", errors.ToArray()) : null;
    }

    /// <summary>联动源按钮预处理（写回每轮、幂等）：
    ///  ① 唯一命名 <原名>_BL&lt;n&gt;——BLRelay 的 N:/X: 段与运行期 GameObject.Find
    ///     都以 GO 名为身份键，而按钮实例名全是 "Switch (N)"（Unity 自动后缀随
    ///     删除/重摆漂移；跨父级重名时 Find 还有歧义——2026-09-28 场景实测互锁
    ///     helper tag 的 N:Switch 与实际按钮名 Switch (2)/(3) 完全脱节）。已带
    ///     _BLn 名保留原号（跨写回稳定），新按钮接全场景最大号。
    ///  ② 停用 SwitchReenable 自动复位（ButtonLink 按钮的 Disable/Reset 由 relay
    ///     按状态机统一投递：Run 离开 Reset / 互锁换手一升一降）。</summary>
    private static void PrepareLinkSources(LayoutButtonLinkDto[] links,
        Dictionary<string, GameObject> createdObjects)
    {
        var sources = new List<GameObject>();
        var seen = new HashSet<int>();
        foreach (var link in links)
        {
            if (link == null || string.IsNullOrEmpty(link.sourceId)) continue;
            if ((link.groupNames == null || link.groupNames.Length == 0) && !HasButtonRelation(link)) continue;
            CollectSource(link.sourceId, createdObjects, sources, seen);
            if (link.sharedSourceIds != null)
            {
                for (int i = 0; i < link.sharedSourceIds.Length; i++)
                    CollectSource(link.sharedSourceIds[i], createdObjects, sources, seen);
            }
        }
        if (sources.Count == 0)
            return;

        int renamed = EnsureUniqueLinkSourceNames(sources);

        int disarmed = 0;
        for (int i = 0; i < sources.Count; i++)
        {
            if (sources[i] == null) continue;
            if (LayoutEditorStubIO.DisarmSwitchReenable(sources[i])) disarmed++;
        }

        if (renamed > 0)
            LayoutEditorLog.Log("button link: 联动源唯一命名 × " + renamed +
                "（BLRelay N:/X: 与 Coaxial 的 GameObject.Find 身份通道不再依赖 Switch (N) 自动后缀）");
        if (disarmed > 0)
            LayoutEditorLog.Log("button link: 联动源停用自动复位 × " + disarmed +
                "（Disable/Reset 由 ButtonLogicRelay/CoaxialButtonGroup 统一投递，避免按下侧被提前拉回绿）");
    }

    /// <summary>联动源按钮唯一命名 &lt;原名&gt;_BL&lt;n&gt;（ButtonLink 与 Coaxial 两个
    /// Bakery 共享的身份通道基础）：BLRelay 的 N:/X: 段与运行期 GameObject.Find
    /// 都以 GO 名为键，而按钮实例名全是 "Switch (N)"（Unity 自动后缀随删除/重摆
    /// 漂移）。已带 _BLn 名保留原号（跨写回稳定），新按钮接全场景最大号。
    /// 返回重命名个数。</summary>
    internal static int EnsureUniqueLinkSourceNames(List<GameObject> sources)
    {
        int renamed = 0;
        if (HasUnnestedName(sources))
        {
            // 全场景扫描既有 _BLn 号取 max（含非联动源残留，避免撞号）。
            int max = 0;
            foreach (var t in UnityEngine.Object.FindObjectsOfType<Transform>())
            {
                if (t == null) continue;
                int n = ParseBlSuffix(t.name);
                if (n > max) max = n;
            }
            for (int i = 0; i < sources.Count; i++)
            {
                var go = sources[i];
                if (go == null || ParseBlSuffix(go.name) > 0) continue; // 已唯一命名，保留原号
                var m = System.Text.RegularExpressions.Regex.Match(go.name, @"^(.*?)\s*\(\d+\)$");
                var baseName = m.Success ? m.Groups[1].Value : go.name;
                max++;
                Undo.RecordObject(go, "Layout Editor Button Link Source Name");
                go.name = baseName + "_BL" + max;
                renamed++;
            }
        }
        return renamed;
    }

    private static void CollectSource(string sid, Dictionary<string, GameObject> createdObjects,
        List<GameObject> sources, HashSet<int> seen)
    {
        if (string.IsNullOrEmpty(sid)) return;
        var go = ResolveObject(sid, createdObjects);
        if (go == null) return;
        if (seen.Add(go.GetInstanceID()))
            sources.Add(go);
    }

    /// <summary>是否还有未带 _BLn 名的联动源（全场景 Transform 扫描只在需要时做）。</summary>
    private static bool HasUnnestedName(List<GameObject> sources)
    {
        for (int i = 0; i < sources.Count; i++)
        {
            if (sources[i] != null && ParseBlSuffix(sources[i].name) == 0)
                return true;
        }
        return false;
    }

    /// <summary>解析名字尾部 _BL&lt;n&gt; 序号（无则 0）。</summary>
    private static int ParseBlSuffix(string name)
    {
        if (string.IsNullOrEmpty(name)) return 0;
        var idx = name.LastIndexOf("_BL", StringComparison.Ordinal);
        if (idx <= 0) return 0;
        int n;
        return int.TryParse(name.Substring(idx + 3), out n) ? n : 0;
    }

    /// <summary>解析 link 的动画组根物体列表（跳过无法解析的项，返回 null 表示有缺失）。
    ///  组绑定去重按 owner：互锁对（同 pairId）双方绑同一组 = 共享环，第二条链路
    ///  允许重复解析同一根（触发名复用 A 侧）。</summary>
    private static List<Transform> ResolveGroupRoots(LayoutButtonLinkDto link, int count,
        Dictionary<string, AnimGroupDto> byName, HashSet<string> boundGroups,
        List<AnimGroupDto> outGroups, Dictionary<string, string> groupOwner)
    {
        var roots = new List<Transform>();
        for (int i = 0; i < count; i++)
        {
            var gname = link.groupNames[i];
            AnimGroupDto g;
            if (string.IsNullOrEmpty(gname) || !byName.TryGetValue(gname, out g))
            {
                outGroups.Add(null);
                roots.Add(null);
                continue;
            }
            if (boundGroups.Contains(gname))
            {
                string owner = null;
                if (groupOwner != null && !groupOwner.TryGetValue(gname, out owner))
                    owner = null;
                bool samePair = !string.IsNullOrEmpty(link.pairId) && owner == link.pairId;
                if (!samePair)
                {
                    outGroups.Add(null);
                    roots.Add(null);
                    continue;
                }
                // 共享环：重复解析同一组（幂等），不重复登记
            }
            else
            {
                boundGroups.Add(gname);
                if (groupOwner != null)
                    groupOwner[gname] = link.pairId;
            }
            outGroups.Add(g);
            roots.Add(ResolveGroupRoot(g));
        }
        return roots;
    }

    private static string BakeSequence(Scene scene, LayoutButtonLinkDto link,
        Dictionary<string, AnimGroupDto> byName, HashSet<string> boundGroups,
        string animDir, string sceneName, Dictionary<string, GameObject> createdObjects,
        HashSet<string> usedHelpers, HashSet<string> usedAssets)
    {
        var helperName = HelperNameForSeq(link);
        var sourceGo = ResolveObject(link.sourceId, createdObjects);
        if (sourceGo == null)
            return "按钮联动：找不到触发源 " + link.sourceId;

        var groups = new List<AnimGroupDto>();
        var roots = ResolveGroupRoots(link, link.groupNames.Length, byName, boundGroups, groups, null);
        for (int i = 0; i < roots.Count; i++)
        {
            if (roots[i] == null)
                return "按钮联动：动画组「" + link.groupNames[i] + "」无法绑定（不存在/未烘焙/已被其他联动占用）";
        }

        var helper = EnsureHelper(helperName);
        if (helper == null)
            return "按钮联动：无法创建 " + ButtonLogicRootPath + "/" + helperName;
        usedHelpers.Add(helperName);

        int n = roots.Count;
        // 无绑定组（纯共轭双按钮）：合成 0.35s 脉冲完成信号——Run 进入广播
        // Disable（双锁）、脉冲结束回 Ready 广播 Reset（同步红闪后一起回绿）。
        // 原 toggle 式「再按抬起」不可行：Disable 后 Interactable 已禁用，无法再按。
        bool pulse = n == 0;
        var goTrigs = new string[n];
        var doneTrigs = pulse ? new string[] { DoneTrigger(helperName, "", 0) } : new string[n];
        var rootNames = new string[n];
        for (int i = 0; i < n; i++)
        {
            goTrigs[i] = GoTrigger(helperName, "", i);
            doneTrigs[i] = DoneTrigger(helperName, "", i);
            rootNames[i] = roots[i].name;
        }

        var controllerPath = animDir + "/" + sceneName + "_" + helperName + ".controller";
        AnimatorController controller;
        if (n == 0 || link.simultaneous)
        {
            // 同按模式：单步环 Ready --BLAdv--> Run（进入即同时启动全部组）
            // --BLDone_0--> Ready（最快组完成即解锁；时长一致即同时完成）；
            // 无绑定组时 BLDone_0 由 0.35s 合成脉冲 clip 产生。
            controller = BuildSimultaneousController(controllerPath, helperName, doneTrigs,
                animDir, usedAssets, pulse);
        }
        else
        {
            controller = BuildSequenceController(controllerPath, helperName, rootNames, goTrigs, doneTrigs,
                link.lockUntilFinished, link.sequenceMode);
        }
        if (controller == null)
            return "按钮联动：controller 创建失败 " + controllerPath;
        usedAssets.Add(controllerPath);

        var anim = AttachHelperAnimator(helper, controller);
        RebuildDoneRelays(helper.gameObject, anim, doneTrigs);
        for (int i = 0; i < n; i++)
            WireGroupQueue(roots[i], doneTrigs[i], helper.gameObject);

        // 状态→组分发：优先 ButtonLogicRelay 场景组件（持久化可靠）；类型缺失时
        // 回退烘焙 SMB（Unity 2017.4 上不可靠，仅防御）。
        var order = new List<int>();
        for (int i = 0; i < n; i++) order.Add(i);
        if (!link.simultaneous && link.sequenceMode == "pingpong" && n > 2)
        {
            for (int i = n - 2; i > 0; i--) order.Add(i);
        }
        var stateNames = new List<string>();
        var relayGo = new List<string>();
        var relayTargets = new List<string>();
        if (pulse)
        {
            // 无绑定组共轭：relay 必须能识别 Run 态——进入 Run 广播 Disable（按一个
            // 双锁）、脉冲结束回 Ready 广播 Reset。此前 stateNames 为空导致 relay
            // 永远检测不到 Run，「另一个按钮不会动」的直接根因（2026-09-28）。
            stateNames.Add("Run");
        }
        else if (link.simultaneous)
        {
            // 同按：全部组都挂在同一个 "Run" 状态上（relay 对同状态多条目逐条分发）。
            for (int i = 0; i < n; i++)
            {
                stateNames.Add("Run");
                relayGo.Add(goTrigs[i]);
                relayTargets.Add(rootNames[i]);
            }
        }
        else
        {
            for (int i = 0; i < order.Count; i++)
            {
                stateNames.Add("Run_" + i);
                relayGo.Add(goTrigs[order[i]]);
                relayTargets.Add(rootNames[order[i]]);
            }
        }
        // 锁定模式的按压封禁状态 = 运行态；非锁定 = 不封禁（环语义有直连出口/排队）。
        var blocked = new List<string>();
        if (link.lockUntilFinished)
        {
            blocked.Add((pulse || link.simultaneous) ? "Run" : "Run_" + 0);
            if (!pulse && !link.simultaneous)
                for (int i = 1; i < order.Count; i++) blocked.Add("Run_" + i);
        }
        // 配对按钮（主源 + 共控）：relay 在 Run 进入/离开时对它们发 Disable/Reset
        // （真机上这是纯 ButtonLink 按钮唯一的回绿通道 + 共轭「按一个双锁」语义）。
        var buttonRoots = new List<string>();
        buttonRoots.Add(sourceGo.name);
        foreach (var sid in link.sharedSourceIds ?? new string[0])
        {
            if (string.IsNullOrEmpty(sid) || sid == link.sourceId) continue;
            var sharedGo = ResolveObject(sid, createdObjects);
            if (sharedGo == null)
            {
                LayoutEditorLog.LogWarning("button link: 共控按钮不在场景中，跳过 " + sid);
                continue;
            }
            if (!buttonRoots.Contains(sharedGo.name))
                buttonRoots.Add(sharedGo.name);
        }
        if (!AttachGoRelay(helper.gameObject, anim,
            new[] { AdvanceTrigger(helperName) },
            new[] { string.Join(",", blocked.ToArray()) },
            stateNames.ToArray(), relayGo.ToArray(), relayTargets.ToArray(), doneTrigs,
            buttonRoots.ToArray()))
        {
            if (link.simultaneous)
                AttachSmbSimultaneousFallback(controller, rootNames, goTrigs,
                    link.lockUntilFinished, AdvanceTrigger(helperName));
            else
                AttachSmbFallback(controller, order, rootNames, goTrigs, doneTrigs,
                    link.lockUntilFinished, AdvanceTrigger(helperName));
        }
        WireSource(sourceGo, AdvanceTrigger(helperName), anim);
        // 共控按钮：与主源同款接线（同一 BLAdv → 同一 helper 环，任一按压都推进）。
        foreach (var sid in link.sharedSourceIds ?? new string[0])
        {
            if (string.IsNullOrEmpty(sid) || sid == link.sourceId) continue;
            var sharedGo = ResolveObject(sid, createdObjects);
            if (sharedGo == null) continue;
            WireSource(sharedGo, AdvanceTrigger(helperName), anim);
        }
        return null;
    }

    private static string BakePair(Scene scene, LayoutButtonLinkDto linkA, LayoutButtonLinkDto linkB,
        Dictionary<string, AnimGroupDto> byName, HashSet<string> boundGroups,
        Dictionary<string, string> groupOwner,
        string animDir, string sceneName, Dictionary<string, GameObject> createdObjects,
        HashSet<string> usedHelpers, HashSet<string> usedAssets)
    {
        var helperName = HelperNameForPair(linkA.pairId);
        var srcA = ResolveObject(linkA.sourceId, createdObjects);
        var srcB = ResolveObject(linkB.sourceId, createdObjects);
        if (srcA == null || srcB == null)
            return "互锁按钮：找不到触发源（A=" + linkA.sourceId + " B=" + linkB.sourceId + "）";

        int nA = Math.Min(linkA.groupNames.Length, PairGroupLimit);
        int nB = Math.Min(linkB.groupNames.Length, PairGroupLimit);
        var groupsA = new List<AnimGroupDto>();
        var groupsB = new List<AnimGroupDto>();
        var rootsA = ResolveGroupRoots(linkA, nA, byName, boundGroups, groupsA, groupOwner);
        var rootsB = ResolveGroupRoots(linkB, nB, byName, boundGroups, groupsB, groupOwner);
        for (int i = 0; i < rootsA.Count; i++)
            if (rootsA[i] == null) return "互锁按钮：动画组「" + linkA.groupNames[i] + "」无法绑定（不存在/未烘焙/已被占用）";
        for (int i = 0; i < rootsB.Count; i++)
            if (rootsB[i] == null) return "互锁按钮：动画组「" + linkB.groupNames[i] + "」无法绑定（不存在/未烘焙/已被占用）";

        // 共享环模式：双方绑【同一组集合】（如传送带阵 ×4 + 互锁双按钮）——
        // ARun/BRun 分发同一组触发（A 侧名），节点环按按压交替推进（A 恒 +90°、
        // B 恒 −90°），完成信号复用 A 侧 BLDone（BRun 出口同名）。
        bool sharedGroups = nA > 0 && nA == nB && SameGroupSet(linkA, nA, linkB, nB);

        var helper = EnsureHelper(helperName);
        if (helper == null)
            return "互锁按钮：无法创建 " + ButtonLogicRootPath + "/" + helperName;
        usedHelpers.Add(helperName);

        // 无绑定组（纯互锁双按钮）：换手合成 0.35s 脉冲完成信号——按下方→进入
        // ARun/BRun 脉冲（双红），脉冲结束才换对方抬起。此前的 AReady--PA-->BReady
        // 瞬时换手零防抖，连按可每帧来回翻（2026-09-28 防抖需求）。
        bool pulseA = nA == 0;
        bool pulseB = nB == 0;
        var goA = pulseA ? new string[1] : new string[nA];
        var doneA = pulseA ? new string[] { DoneTrigger(helperName, "A", 0) } : new string[nA];
        var namesA = pulseA ? new string[1] : new string[nA];
        var goB = pulseB ? new string[1] : new string[nB];
        var doneB = pulseB ? new string[] { DoneTrigger(helperName, "B", 0) } : new string[nB];
        var namesB = pulseB ? new string[1] : new string[nB];
        if (pulseA) { goA[0] = ""; namesA[0] = ""; }
        if (pulseB) { goB[0] = ""; namesB[0] = ""; }
        for (int i = 0; i < nA; i++)
        {
            goA[i] = GoTrigger(helperName, "A", i);
            doneA[i] = DoneTrigger(helperName, "A", i);
            namesA[i] = rootsA[i].name;
        }
        for (int i = 0; i < nB; i++)
        {
            goB[i] = GoTrigger(helperName, "B", i);
            doneB[i] = DoneTrigger(helperName, "B", i);
            namesB[i] = rootsB[i].name;
        }
        if (sharedGroups)
        {
            // B 侧复用 A 侧触发/目标/完成名（BRun 分发同一 BLGo、出口同一 BLDone）。
            goB = (string[])goA.Clone();
            doneB = (string[])doneA.Clone();
            namesB = (string[])namesA.Clone();
        }

        // 初始抬起方：linkA.pairStartsUp=true → A 抬起、B 按下。
        bool aStartsUp = linkA.pairStartsUp;

        var controllerPath = animDir + "/" + sceneName + "_" + helperName + ".controller";
        var pulseClipA = pulseA
            ? BuildPulseClip(controllerPath, doneA[0], animDir, usedAssets, "_A")
            : null;
        var pulseClipB = pulseB
            ? BuildPulseClip(controllerPath, doneB[0], animDir, usedAssets, "_B")
            : null;
        var controller = BuildPairController(controllerPath, helperName, namesA, goA, doneA,
            namesB, goB, doneB, aStartsUp, pulseClipA, pulseClipB);
        if (controller == null)
            return "互锁按钮：controller 创建失败 " + controllerPath;
        usedAssets.Add(controllerPath);

        var anim = AttachHelperAnimator(helper, controller);
        // 完成名去重（共享环时 A/B 同名）：重复中继会对同一 BLDone 双投递。
        var allDone = new List<string>();
        foreach (var t in doneA)
            if (!allDone.Contains(t)) allDone.Add(t);
        foreach (var t in doneB)
            if (!allDone.Contains(t)) allDone.Add(t);
        RebuildDoneRelays(helper.gameObject, anim, allDone.ToArray());
        for (int i = 0; i < nA; i++) WireGroupQueue(rootsA[i], doneA[i], helper.gameObject);
        for (int i = 0; i < nB; i++) WireGroupQueue(rootsB[i], doneB[i], helper.gameObject);

        // relay 分发：ARun 同时启动 A 方各组、BRun 启动 B 方各组；
        // 互斥封禁 = 对方就绪态 + 双方运行态（原 ClearTrigger SMB 的职责）。
        var stateNames = new List<string>();
        var relayGo = new List<string>();
        var relayTargets = new List<string>();
        // 无绑定组脉冲：Run 态必须进 stateNames，relay 才能在脉冲期间双锁按钮、
        // 并在换手时投递互锁视觉（与共轭无组同款断链教训）。
        if (pulseA) { stateNames.Add("ARun"); relayGo.Add(""); relayTargets.Add(""); }
        if (pulseB) { stateNames.Add("BRun"); relayGo.Add(""); relayTargets.Add(""); }
        for (int i = 0; i < nA; i++)
        {
            stateNames.Add("ARun");
            relayGo.Add(goA[i]);
            relayTargets.Add(namesA[i]);
        }
        for (int i = 0; i < nB; i++)
        {
            stateNames.Add("BRun");
            relayGo.Add(goB[i]);
            relayTargets.Add(namesB[i]);
        }
        // 互斥封禁：对方就绪态 + 双方运行态（本侧就绪态须能按压）。
        var blockedA = "BReady,ARun,BRun,AWait1,AWait0";
        var blockedB = "AReady,BRun,ARun,BWait1,BWait0";
        bool relayOk = AttachGoRelay(helper.gameObject, anim,
            new[] { PressTrigger(helperName, "A"), PressTrigger(helperName, "B") },
            new[] { blockedA, blockedB },
            stateNames.ToArray(), relayGo.ToArray(), relayTargets.ToArray(), allDone.ToArray(),
            new[] { srcA.name, srcB.name }, srcA.name, srcB.name);
        if (!relayOk)
        {
            AttachSmbPairFallback(controller, namesA, goA, namesB, goB);
        }
        else
        {
            // 防退化校验 + 烘焙摘要：互锁配置真机自愈的唯一来源是 |X:A,B tag，
            // 缺失即运行期退化为共轭（同时锁/同时解锁）——写回后 Console 逐条可见。
            var blTag = helper.GetComponent<LevelEditorStub.SpecificPseudoPrefabTag>();
            var tagStr = blTag != null ? blTag.prefabTag : null;
            if (string.IsNullOrEmpty(tagStr) || tagStr.IndexOf("|X:") < 0)
            {
                LayoutEditorLog.LogWarning("button link: ⚠ 互锁 helper " + helperName +
                    " 的 BLRelay tag 缺少 |X:" + srcA.name + "," + srcB.name +
                    "——真机将退化为共轭，请重新写回；若反复出现请检查 WriteRelayTag");
            }
            else
            {
                LayoutEditorLog.Log("button link: 互锁 helper " + helperName + " 烘焙完成：A=" +
                    srcA.name + "（" + nA + " 组）B=" + srcB.name + "（" + nB + " 组）初始抬起=" +
                    (aStartsUp ? "A" : "B") + (sharedGroups ? "【共享环：同组分发，A/B 交替推进】" : "") +
                    "，tag=" + tagStr);
            }
        }
        WireSource(srcA, PressTrigger(helperName, "A"), anim);
        WireSource(srcB, PressTrigger(helperName, "B"), anim);
        return null;
    }

    // ---------------------------------------------------------------- components

    /// <summary>在 helper 上挂/重建 ButtonLogicRelay（CustomStub.ButtonLogicRelay，
    /// WebCustomStubRuntime，反射挂载）。返回 false = 类型不可用（调用方回退 SMB）。</summary>
    private static bool AttachGoRelay(GameObject helper, Animator anim,
        string[] pressTriggers, string[] pressBlockedStates,
        string[] stateNames, string[] goTriggers, string[] targetNames, string[] doneTriggers,
        string[] buttonRootNames, string interlockSideA = null, string interlockSideB = null)
    {
        var relayType = LayoutEditorStubIO.FindCustomStubType(helper, "ButtonLogicRelay");
        if (relayType == null)
        {
            LayoutEditorLog.LogWarning("button link: 找不到 CustomStub.ButtonLogicRelay" +
                "（WebCustomStubRuntime 未编译？）——回退 controller 内嵌 SMB 分发" +
                "（Unity 2017.4 上可能不持久化，按钮联动可能在重载后失效）");
            return false;
        }
        foreach (var old in helper.GetComponents(relayType))
            Undo.DestroyObjectImmediate(old);
        var relay = Undo.AddComponent(helper, relayType);
        SetRelayField(relay, "m_pressTriggers", pressTriggers);
        SetRelayField(relay, "m_pressBlockedStates", pressBlockedStates);
        SetRelayField(relay, "m_stateNames", stateNames);
        SetRelayField(relay, "m_goTriggers", goTriggers);
        SetRelayField(relay, "m_targetNames", targetNames);
        SetRelayField(relay, "m_doneTriggers", doneTriggers);
        SetRelayField(relay, "m_buttonRootNames", buttonRootNames ?? new string[0]);
        SetRelayBool(relay, "m_interlockPair", !string.IsNullOrEmpty(interlockSideA));
        SetRelayString(relay, "m_interlockSideA", interlockSideA);
        SetRelayString(relay, "m_interlockSideB", interlockSideB);
        var rebuild = relay.GetType().GetMethod("RebuildHashes", System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance);
        if (rebuild != null)
            rebuild.Invoke(relay, null);
        EditorUtility.SetDirty(relay);
        // 真机自愈载体：CustomStub 组件按脚本 GUID 序列化，游戏侧无此脚本注册表，
        // 烘焙件在真机是 Missing Script 死件——配置必须编码进 tag，由 EntryPoint
        // 运行时补挂（与 RandomCrate/SwitchReenable 同一可靠通道）。
        WriteRelayTag(helper, pressTriggers, pressBlockedStates, stateNames, goTriggers, targetNames, doneTriggers, buttonRootNames, interlockSideA, interlockSideB);
        LayoutEditorLog.Log("button link: helper " + helper.name + " 挂载 ButtonLogicRelay（" +
            stateNames.Length + " 条分发：" + string.Join("/", stateNames) + " → " +
            string.Join("/", targetNames) + "）");
        return true;
    }

    /// <summary>写 BLRelay 自愈 tag（单组件约定：helper 上无其他 tag，可整串覆写）。
    /// 格式 BLRelay|P:按压名,..|B:封禁1;封禁2|E:状态>触发>目标;..|D:完成名,..|N:按钮根名,..
    /// 字段内 %,;>| 做 %XX 转义（用户组名可能含任意字符）。EntryPoint 解析回填。</summary>
    private static void WriteRelayTag(GameObject helper,
        string[] pressTriggers, string[] pressBlockedStates,
        string[] stateNames, string[] goTriggers, string[] targetNames, string[] doneTriggers,
        string[] buttonRootNames, string interlockSideA = null, string interlockSideB = null)
    {
        try
        {
            var sb = new System.Text.StringBuilder("BLRelay");
            sb.Append("|P:").Append(JoinEsc(pressTriggers, ','));
            sb.Append("|B:").Append(JoinEsc(pressBlockedStates, ';'));
            sb.Append("|E:");
            for (int i = 0; i < stateNames.Length; i++)
            {
                if (i > 0) sb.Append(';');
                sb.Append(EscTag(stateNames[i])).Append('>')
                  .Append(EscTag(i < goTriggers.Length ? goTriggers[i] : ""))
                  .Append('>')
                  .Append(EscTag(i < targetNames.Length ? targetNames[i] : ""));
            }
            sb.Append("|D:").Append(JoinEsc(doneTriggers, ','));
            sb.Append("|N:").Append(JoinEsc(buttonRootNames, ','));
            if (!string.IsNullOrEmpty(interlockSideA) && !string.IsNullOrEmpty(interlockSideB))
                sb.Append("|X:").Append(EscTag(interlockSideA)).Append(',').Append(EscTag(interlockSideB));
            var tag = helper.GetComponent<LevelEditorStub.SpecificPseudoPrefabTag>();
            if (tag == null)
            {
                tag = Undo.AddComponent<LevelEditorStub.SpecificPseudoPrefabTag>(helper);
            }
            else
            {
                Undo.RecordObject(tag, "Layout Editor Button Link Relay Tag");
            }
            tag.prefabTag = sb.ToString();
            EditorUtility.SetDirty(tag);
        }
        catch (Exception ex)
        {
            // tag 失败仅损失真机自愈通道（编辑器 Play 不受影响），记录后继续。
            LayoutEditorLog.LogWarning("button link: BLRelay tag 写入失败 " + helper.name + ": " + ex.Message);
        }
    }

    internal static string JoinEsc(string[] parts, char sep)
    {
        var sb = new System.Text.StringBuilder();
        if (parts == null) return sb.ToString();
        for (int i = 0; i < parts.Length; i++)
        {
            if (i > 0) sb.Append(sep);
            sb.Append(EscTag(parts[i]));
        }
        return sb.ToString();
    }

    /// <summary>tag 字段转义：%,;>| 保留字符 → %XX（EntryPoint 侧对称解码；
    /// CoaxialButtonBakery 共享）。</summary>
    internal static string EscTag(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("%", "%25").Replace(",", "%2C").Replace(";", "%3B")
            .Replace(">", "%3E").Replace("|", "%7C");
    }

    private static void SetRelayField(Component relay, string field, string[] value)
    {
        var f = relay.GetType().GetField(field, System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance);
        if (f != null)
        {
            Undo.RecordObject(relay, "Layout Editor Button Link Relay");
            f.SetValue(relay, value);
        }
    }

    private static void SetRelayBool(Component relay, string field, bool value)
    {
        var f = relay.GetType().GetField(field, System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance);
        if (f != null)
        {
            Undo.RecordObject(relay, "Layout Editor Button Link Relay");
            f.SetValue(relay, value);
        }
    }

    private static void SetRelayString(Component relay, string field, string value)
    {
        var f = relay.GetType().GetField(field, System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance);
        if (f != null)
        {
            Undo.RecordObject(relay, "Layout Editor Button Link Relay");
            f.SetValue(relay, value);
        }
    }

    /// <summary>relay 不可用时的防御路径：给已建 controller 的 Run_i 状态补
    ///  SendTriggerToObject / ClearTriggerDuringState SMB（旧方案，可能不持久化）。</summary>
    private static void AttachSmbFallback(AnimatorController controller, List<int> order,
        string[] rootNames, string[] goTrigs, string[] doneTrigs, bool lockUntilFinished,
        string advTrigger)
    {
        var sm = controller.layers[0].stateMachine;
        foreach (var cs in sm.states)
        {
            var st = cs.state;
            if (st == null || !st.name.StartsWith("Run_", StringComparison.Ordinal)) continue;
            int idx;
            if (!int.TryParse(st.name.Substring(4), out idx) || idx >= order.Count) continue;
            AddSendTrigger(st, rootNames[order[idx]], goTrigs[order[idx]]);
            if (lockUntilFinished)
                AddClearTrigger(st, advTrigger);
        }
        LayoutEditorLog.LogWarning("button link: 已回退 SMB 分发（ButtonLogicRelay 不可用）");
    }

    /// <summary>同按模式的 SMB 兜底（relay 不可用时）：全部组挂在单一 Run 状态。</summary>
    private static void AttachSmbSimultaneousFallback(AnimatorController controller,
        string[] rootNames, string[] goTrigs, bool lockUntilFinished, string advTrigger)
    {
        var sm = controller.layers[0].stateMachine;
        foreach (var cs in sm.states)
        {
            var st = cs.state;
            if (st == null || st.name != "Run") continue;
            for (int i = 0; i < rootNames.Length; i++)
                AddSendTrigger(st, rootNames[i], goTrigs[i]);
            if (lockUntilFinished)
                AddClearTrigger(st, advTrigger);
        }
        LayoutEditorLog.LogWarning("button link: 同按模式已回退 SMB 分发（ButtonLogicRelay 不可用）");
    }

    private static void AttachSmbPairFallback(AnimatorController controller,
        string[] namesA, string[] goA, string[] namesB, string[] goB)
    {
        var sm = controller.layers[0].stateMachine;
        foreach (var cs in sm.states)
        {
            var st = cs.state;
            if (st == null) continue;
            if (st.name == "ARun")
            {
                for (int i = 0; i < namesA.Length; i++) AddSendTrigger(st, namesA[i], goA[i]);
            }
            else if (st.name == "BRun")
            {
                for (int i = 0; i < namesB.Length; i++) AddSendTrigger(st, namesB[i], goB[i]);
            }
        }
        LayoutEditorLog.LogWarning("button link: 互锁已回退 SMB 分发（ButtonLogicRelay 不可用）");
    }

    private static Transform EnsureHelper(string helperName)
    {
        var t = LayoutEditorHierarchy.FindOrCreatePath(ButtonLogicRootPath + "/" + helperName);
        if (t != null)
        {
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
        }
        return t;
    }

    private static Animator AttachHelperAnimator(Transform helper, AnimatorController controller)
    {
        var go = helper.gameObject;
        var anim = go.GetComponent<Animator>();
        if (anim == null)
            anim = Undo.AddComponent<Animator>(go);
        else
            Undo.RecordObject(anim, "Layout Editor Button Link");
        anim.runtimeAnimatorController = controller;
        anim.applyRootMotion = false;
        EditorUtility.SetDirty(anim);
        return anim;
    }

    /// <summary>重建 helper 上的 Done 消息 → Animator trigger 中继（组完成时
    ///  TriggerQueue 向 helper 广播 endTrigger，TriggerOnAnimator 转成状态机 trigger）。</summary>
    private static void RebuildDoneRelays(GameObject helper, Animator anim, string[] doneTriggers)
    {
        foreach (var old in helper.GetComponents<TriggerOnAnimator>())
            Undo.DestroyObjectImmediate(old);
        foreach (var t in doneTriggers)
        {
            if (string.IsNullOrEmpty(t)) continue;
            var relay = Undo.AddComponent<TriggerOnAnimator>(helper);
            relay.m_triggerToReceive = t;
            relay.m_triggerToFire = t;
            relay.m_targetAnimator = anim;
            relay.m_triggerToFireHash = Animator.StringToHash(t);
            EditorUtility.SetDirty(relay);
        }
    }

    /// <summary>把组根的 TriggerQueue 完成事件指向 helper（m_endTrigger 已由
    ///  PrepareGroups→AnimGroupBakery 写入，这里补 m_endTriggerTarget 并核对）。</summary>
    private static void WireGroupQueue(Transform groupRoot, string doneTrigger, GameObject helper)
    {
        var q = groupRoot.GetComponent<TriggerQueue>();
        if (q == null)
        {
            LayoutEditorLog.LogWarning("button link: 组根 " + groupRoot.name + " 缺少 TriggerQueue");
            return;
        }
        Undo.RecordObject(q, "Layout Editor Button Link");
        q.m_endTrigger = doneTrigger;
        q.m_endTriggerTarget = helper;
        EditorUtility.SetDirty(q);
    }

    /// <summary>写触发源 stub：Switch 用 triggerOnAnimator/animatorToTrigger（按压驱动
    ///  逻辑 Animator）；PressureSwitch 用 triggerOnAnimatorEnter（Exit 指向 BLNoop 占位）；
    ///  ToggleSwitch（拨动开关）与 Switch 同款 animator 通道（PseudoPrefabToggleSwitch.Setup
    ///  会在 child 上接线 TriggerOnAnimator，m_triggerToReceive="Switch"）。</summary>
    private static void WireSource(GameObject sourceGo, string pressTrigger, Animator targetAnim)
    {
        var sw = sourceGo.GetComponent<PseudoPrefabSwitchStub>();
        if (sw != null)
        {
            Undo.RecordObject(sw, "Layout Editor Button Link");
            sw.triggerOnAnimator = pressTrigger;
            sw.animatorToTrigger = targetAnim;
            EditorUtility.SetDirty(sw);
            return;
        }
        var toggleSw = sourceGo.GetComponent<PseudoPrefabToggleSwitchStub>();
        if (toggleSw != null)
        {
            Undo.RecordObject(toggleSw, "Layout Editor Button Link");
            toggleSw.triggerOnAnimator = pressTrigger;
            toggleSw.animatorToTrigger = targetAnim;
            EditorUtility.SetDirty(toggleSw);
            return;
        }
        var ps = sourceGo.GetComponent<PseudoPrefabPressureSwitchStub>();
        if (ps != null)
        {
            Undo.RecordObject(ps, "Layout Editor Button Link");
            ps.triggerOnAnimatorEnter = pressTrigger;
            ps.triggerOnAnimatorExit = NoopTrigger;
            ps.animatorToTrigger = targetAnim;
            EditorUtility.SetDirty(ps);
            return;
        }
        LayoutEditorLog.LogWarning("button link: 触发源 " + sourceGo.name +
            " 上没有 Switch/ToggleSwitch/PressureSwitch stub，无法接线");
    }

    // ---------------------------------------------------------------- controller

    private static bool HasButtonRelation(LayoutButtonLinkDto link)
    {
        if (link == null) return false;
        if (link.sharedSourceIds != null && link.sharedSourceIds.Length > 0) return true;
        return !string.IsNullOrEmpty(link.pairId);
    }

    private static AnimatorStateTransition AddTrigTransition(AnimatorState from, AnimatorState to, string trigger)
    {
        var tr = from.AddTransition(to);
        tr.hasExitTime = false;
        tr.duration = 0f;
        tr.hasFixedDuration = true;
        tr.AddCondition(AnimatorConditionMode.If, 0f, trigger);
        return tr;
    }

    /// <summary>无绑定动画组时 Run 立即回到 Ready（仅共轭/互锁按钮接线）。</summary>
    private static void AddInstantTransition(AnimatorState from, AnimatorState to)
    {
        var tr = from.AddTransition(to);
        tr.hasExitTime = true;
        tr.exitTime = 0f;
        tr.duration = 0f;
        tr.hasFixedDuration = true;
    }

    private static void AddSendTrigger(AnimatorState st, string objectName, string triggerToSend)
    {
        var smb = st.AddStateMachineBehaviour<SendTriggerToObject>();
        smb.name = "Send_" + triggerToSend;
        var so = new SerializedObject(smb);
        SetString(so, "m_objectName", objectName);
        SetString(so, "m_triggerToSend", triggerToSend);
        var tt = so.FindProperty("m_triggerTime");
        if (tt != null) tt.floatValue = 0f;
        var oe = so.FindProperty("m_orTriggerOnExit");
        if (oe != null) oe.boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AddClearTrigger(AnimatorState st, string triggerName)
    {
        var smb = st.AddStateMachineBehaviour<ClearTriggerDuringState>();
        smb.name = "Clear_" + triggerName;
        var so = new SerializedObject(smb);
        SetString(so, "m_triggerName", triggerName);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetString(SerializedObject so, string prop, string value)
    {
        var p = so.FindProperty(prop);
        if (p != null) p.stringValue = value;
    }

    private static AnimatorState NewState(AnimatorStateMachine sm, string name)
    {
        var st = sm.AddState(name);
        st.writeDefaultValues = false;
        return st;
    }

    /// <summary>顺序联动状态机：Ready_i --Advance--> Run_i --Done_i--> 下一步骤。
    ///  「进入 Run_i 即启动组 i」的分发由 helper 上的 ButtonLogicRelay 场景组件承担
    ///  （controller 内嵌 SendTriggerToObject/ClearTriggerDuringState SMB 在 Unity
    ///  2017.4 资产往返中不可靠持久化——2026-09-24 事故：回导恒空 + 运行期无分发，
    ///  是「按钮无法触发动画组」的根因；场景组件的持久性已被 TriggerOnAnimator
    ///  完成中继实证）。锁定（运行期吞按压）与 done 防锁存同样由 relay 处理。
    ///  relay 类型缺失时由 BakeSequence 回退烘焙 SMB（防御路径）。</summary>
    private static AnimatorController BuildSequenceController(string path, string helperName,
        string[] rootNames, string[] goTrigs, string[] doneTrigs, bool lockUntilFinished,
        string sequenceMode)
    {
        AnimGroupBakery.DeleteAssetIfExists(path);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        if (ctrl == null) return null;
        ctrl.name = Path.GetFileNameWithoutExtension(path);

        var adv = AdvanceTrigger(helperName);
        ctrl.AddParameter(adv, AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter(NoopTrigger, AnimatorControllerParameterType.Trigger);
        for (int i = 0; i < doneTrigs.Length; i++)
            ctrl.AddParameter(doneTrigs[i], AnimatorControllerParameterType.Trigger);

        var sm = ctrl.layers[0].stateMachine;
        int n = rootNames.Length;
        var order = new List<int>();
        for (int i = 0; i < n; i++) order.Add(i);
        if (sequenceMode == "pingpong" && n > 2)
        {
            for (int i = n - 2; i > 0; i--) order.Add(i);
        }

        var ready = new AnimatorState[order.Count];
        var run = new AnimatorState[order.Count];
        for (int i = 0; i < order.Count; i++)
        {
            ready[i] = NewState(sm, "Ready_" + i);
            run[i] = NewState(sm, "Run_" + i);
        }
        sm.defaultState = ready[0];

        for (int i = 0; i < order.Count; i++)
        {
            int sourceIndex = order[i];
            int next = (i + 1) % order.Count;
            AddTrigTransition(ready[i], run[i], adv);
            AddTrigTransition(run[i], ready[next], doneTrigs[sourceIndex]);
            if (!lockUntilFinished)
            {
                // 非锁定：运行中再按直接触发下一组。
                AddTrigTransition(run[i], run[next], adv);
            }
        }
        return ctrl;
    }

    /// <summary>互锁对状态机：AReady --PA--> ARun（同时启动 A 方各组）→ A 方全部完成
    ///  （AND 门，m=2 时两条顺序无关路径）→ BReady --PB--> BRun → … → AReady。
    ///  对方的按压在任意非就绪态都被吞掉（功能上的"按下状态不可再按"）。</summary>
    /// <summary>同按模式状态机：Ready --Advance--> Run（进入即由 relay 同时启动全部
    ///    绑定组，各组独立推进）--BLDone_0--> Ready（最快组完成即解锁；各组时长
    ///    一致时即同时完成）。迟到的其余 BLDone 由 relay 换状态时统一清空防锁存。</summary>
    private static AnimatorController BuildSimultaneousController(string path, string helperName,
        string[] doneTrigs, string animDir, HashSet<string> usedAssets, bool pulse)
    {
        AnimGroupBakery.DeleteAssetIfExists(path);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        if (ctrl == null) return null;
        ctrl.name = Path.GetFileNameWithoutExtension(path);

        var adv = AdvanceTrigger(helperName);
        ctrl.AddParameter(adv, AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter(NoopTrigger, AnimatorControllerParameterType.Trigger);
        for (int i = 0; i < doneTrigs.Length; i++)
            ctrl.AddParameter(doneTrigs[i], AnimatorControllerParameterType.Trigger);

        var sm = ctrl.layers[0].stateMachine;
        var ready = NewState(sm, "Ready");
        var run = NewState(sm, "Run");
        sm.defaultState = ready;
        AddTrigTransition(ready, run, adv);
        // 解锁取第一个完成信号；其余组完成信号稍后到达时已在 Ready（无消费过渡），
        // 由 relay 在下一次状态切换时 ResetTrigger 清空。
        if (pulse)
        {
            // 无绑定组：Run 挂 0.35s 合成脉冲 clip，其完成事件产生 BLDone_0 →
            // Ready（relay 随状态迁移广播 Disable/Reset，双按钮同步红闪回绿）。
            run.motion = BuildPulseClip(path, doneTrigs[0], animDir, usedAssets);
            AddTrigTransition(run, ready, doneTrigs[0]);
        }
        else if (doneTrigs.Length > 0)
            AddTrigTransition(run, ready, doneTrigs[0]);
        return ctrl;
    }

    /// <summary>无绑定组联动的 0.35s 脉冲 clip：helper Transform 常值曲线定长 +
    /// 完成事件（OnTrigger → BLDone）。空 clip 无时长，完成事件永远不会触发，
    /// 必须有曲线撑起 length（helper 本地位置本就是零，常值 0 无副作用）。
    /// suffix 区分互锁 A/B 两侧的 clip 名。</summary>
    private static AnimationClip BuildPulseClip(string controllerPath,
        string doneTrigger, string animDir, HashSet<string> usedAssets, string suffix = "")
    {
        var seconds = 0.35f;
        var clip = new AnimationClip();
        clip.name = Path.GetFileNameWithoutExtension(controllerPath) + suffix + "_pulse";
        clip.legacy = false;
        clip.frameRate = 60f;
        var binding = UnityEditor.EditorCurveBinding.FloatCurve(
            "", typeof(Transform), "m_LocalPosition.x");
        AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Constant(0f, seconds, 0f));
        var animEvent = new AnimationEvent();
        animEvent.functionName = "OnTrigger";
        animEvent.stringParameter = doneTrigger;
        animEvent.time = seconds;
        AnimationUtility.SetAnimationEvents(clip, new AnimationEvent[] { animEvent });
        var clipPath = animDir + "/" + clip.name + ".anim";
        AnimGroupBakery.DeleteAssetIfExists(clipPath);
        AssetDatabase.CreateAsset(clip, clipPath);
        EditorUtility.SetDirty(clip);
        usedAssets.Add(clipPath);
        return clip;
    }

    private static AnimatorController BuildPairController(string path, string helperName,
        string[] namesA, string[] goA, string[] doneA,
        string[] namesB, string[] goB, string[] doneB, bool aStartsUp,
        AnimationClip pulseClipA = null, AnimationClip pulseClipB = null)
    {
        AnimGroupBakery.DeleteAssetIfExists(path);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        if (ctrl == null) return null;
        ctrl.name = Path.GetFileNameWithoutExtension(path);

        var pa = PressTrigger(helperName, "A");
        var pb = PressTrigger(helperName, "B");
        ctrl.AddParameter(pa, AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter(pb, AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter(NoopTrigger, AnimatorControllerParameterType.Trigger);
        // 完成参数去重（共享环时 doneA/doneB 同名，重复 AddParameter 会抛异常）。
        for (int i = 0; i < doneA.Length; i++)
            AddParamIfMissing(ctrl, doneA[i]);
        for (int i = 0; i < doneB.Length; i++)
            AddParamIfMissing(ctrl, doneB[i]);

        var sm = ctrl.layers[0].stateMachine;
        var aReady = NewState(sm, "AReady");
        var aRun = NewState(sm, "ARun");
        var bReady = NewState(sm, "BReady");
        var bRun = NewState(sm, "BRun");
        // 无绑定组脉冲：Run 态挂 0.35s 合成 clip，其完成事件产生该侧 BLDone →
        // 换对方抬起（瞬时换手零防抖，连按可每帧来回翻）。
        if (pulseClipA != null) aRun.motion = pulseClipA;
        if (pulseClipB != null) bRun.motion = pulseClipB;
        sm.defaultState = aStartsUp ? aReady : bReady;

        // 「进入 ARun/BRun 同时启动各方全部组」的分发由 ButtonLogicRelay 承担
        // （SMB 持久化不可靠，见 BuildSequenceController 注释）。对方按压的
        // 吞除同样由 relay 的 lock 机制处理。

        if (doneA.Length == 0)
            AddTrigTransition(aReady, bReady, pa);
        else
            AddTrigTransition(aReady, aRun, pa);
        if (doneB.Length == 0)
            AddTrigTransition(bReady, aReady, pb);
        else
            AddTrigTransition(bReady, bRun, pb);

        // AND 门：A 方各组全部完成 → BReady（m=1 直接过渡；m=2 走两条顺序无关路径）。
        if (doneA.Length == 1)
        {
            AddTrigTransition(aRun, bReady, doneA[0]);
        }
        else if (doneA.Length >= 2)
        {
            var aWait1 = NewState(sm, "AWait1"); // 已收到 A0，等 A1
            var aWait0 = NewState(sm, "AWait0"); // 已收到 A1，等 A0
            AddTrigTransition(aRun, aWait1, doneA[0]);
            AddTrigTransition(aWait1, bReady, doneA[1]);
            AddTrigTransition(aRun, aWait0, doneA[1]);
            AddTrigTransition(aWait0, bReady, doneA[0]);
            AddClearTrigger(aWait0, pa); AddClearTrigger(aWait0, pb);
            AddClearTrigger(aWait1, pa); AddClearTrigger(aWait1, pb);
        }
        if (doneB.Length == 1)
        {
            AddTrigTransition(bRun, aReady, doneB[0]);
        }
        else if (doneB.Length >= 2)
        {
            var bWait1 = NewState(sm, "BWait1");
            var bWait0 = NewState(sm, "BWait0");
            AddTrigTransition(bRun, bWait1, doneB[0]);
            AddTrigTransition(bWait1, aReady, doneB[1]);
            AddTrigTransition(bRun, bWait0, doneB[1]);
            AddTrigTransition(bWait0, aReady, doneB[0]);
            AddClearTrigger(bWait0, pa); AddClearTrigger(bWait0, pb);
            AddClearTrigger(bWait1, pa); AddClearTrigger(bWait1, pb);
        }
        AddClearTrigger(aReady, NoopTrigger);
        return ctrl;
    }

    /// <summary>互锁双方绑定的组集合是否一致（共享环判定；按集合比较，顺序无关）。</summary>
    private static bool SameGroupSet(LayoutButtonLinkDto linkA, int nA, LayoutButtonLinkDto linkB, int nB)
    {
        var setA = new HashSet<string>(StringComparer.Ordinal);
        var setB = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < nA; i++)
            if (!string.IsNullOrEmpty(linkA.groupNames[i])) setA.Add(linkA.groupNames[i]);
        for (int i = 0; i < nB; i++)
            if (!string.IsNullOrEmpty(linkB.groupNames[i])) setB.Add(linkB.groupNames[i]);
        return setA.SetEquals(setB);
    }

    private static void AddParamIfMissing(AnimatorController ctrl, string name)
    {
        if (string.IsNullOrEmpty(name)) return;
        var ps = ctrl.parameters;
        for (int i = 0; i < ps.Length; i++)
            if (ps[i].name == name) return;
        ctrl.AddParameter(name, AnimatorControllerParameterType.Trigger);
    }

    // ------------------------------------------------------------------- cleanup

    private static void CleanupStale(string animDir, string sceneName,
        HashSet<string> usedHelpers, HashSet<string> usedAssets)
    {
        var root = LayoutEditorHierarchy.FindByPath(ButtonLogicRootPath);
        if (root != null)
        {
            var stale = new List<GameObject>();
            for (int i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                if (!usedHelpers.Contains(c.name))
                    stale.Add(c.gameObject);
            }
            foreach (var go in stale)
            {
                LayoutEditorLog.Log("button link: cleanup stale helper " + go.name);
                Undo.DestroyObjectImmediate(go);
            }
            if (root.childCount == 0 && stale.Count > 0)
                Undo.DestroyObjectImmediate(root.gameObject);
        }

        if (!AssetDatabase.IsValidFolder(animDir)) return;
        foreach (var guid in AssetDatabase.FindAssets("t:Object", new[] { animDir }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
            var file = Path.GetFileNameWithoutExtension(path);
            if (string.IsNullOrEmpty(file)) continue;
            if (!file.StartsWith(sceneName + "_BtnLogic", StringComparison.Ordinal) &&
                !file.StartsWith(sceneName + "_BtnPair", StringComparison.Ordinal)) continue;
            if (usedAssets.Contains(path)) continue;
            AssetDatabase.DeleteAsset(path);
        }
    }

    // ------------------------------------------------------------------- import

    /// <summary>从场景重建按钮联动（场景是唯一事实源）：扫描 Design/Button Logic 下的
    ///  helper，重建顺序 = ① ButtonLogicRelay 场景组件的序列化字段（2026-09-24 v3 起
    ///  的权威载体）→ ② 组根 TriggerQueue 接线（m_startTrigger=BLGo_*_i +
    ///  m_endTriggerTarget=helper，纯场景数据）→ ③ controller 内嵌 SMB（旧场景遗留，
    ///  Unity 2017.4 持久化不可靠，仅兜底）。触发源仍按开关 stub 接线反查。</summary>
    public static List<LayoutButtonLinkDto> ImportFromScene(Scene scene,
        List<AnimGroupDto> groups, List<LayoutItemDto> items)
    {
        var result = new List<LayoutButtonLinkDto>();
        if (!scene.IsValid()) return result;
        var root = LayoutEditorHierarchy.FindByPath(ButtonLogicRootPath);
        if (root == null) return result;

        // startTrigger → 动画组（由 PrepareGroups 覆写为 BLGo_* 名，随 AnimGroupImporter 回导）
        var groupByStartTrigger = new Dictionary<string, AnimGroupDto>(StringComparer.Ordinal);
        foreach (var g in groups ?? new List<AnimGroupDto>())
        {
            if (g == null || string.IsNullOrEmpty(g.startTrigger)) continue;
            if (!groupByStartTrigger.ContainsKey(g.startTrigger))
                groupByStartTrigger[g.startTrigger] = g;
        }
        var groupNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var g in groups ?? new List<AnimGroupDto>())
            if (g != null && !string.IsNullOrEmpty(g.displayName))
                groupNames.Add(g.displayName);

        for (int i = 0; i < root.childCount; i++)
        {
            var helper = root.GetChild(i);
            var anim = helper.GetComponent<Animator>();
            var ctrl = anim != null ? anim.runtimeAnimatorController as AnimatorController : null;
            var sm = ctrl != null && ctrl.layers.Length > 0 ? ctrl.layers[0].stateMachine : null;

            var name = helper.name;
            if (name.StartsWith(HelperPairPrefix, StringComparison.Ordinal))
                ImportPair(result, name, helper.gameObject, anim, sm, groupByStartTrigger, groupNames, items);
            else if (name.StartsWith(HelperSeqPrefix, StringComparison.Ordinal) ||
                     name.StartsWith(HelperSeqNoLockPrefix, StringComparison.Ordinal))
                ImportSequence(result, name, helper.gameObject, anim, sm, groupByStartTrigger, groupNames, items);
        }
        return result;
    }

    /// <summary>helper 的组接线条目（重建顺序用）。</summary>
    private class WiringEntry
    {
        public string side; // "" = 顺序；"A"/"B" = 互锁
        public int idx;
        public string groupName;
    }

    /// <summary>① ButtonLogicRelay 组件读取（反射，字段即接线）。</summary>
    private static List<WiringEntry> ReadRelayEntries(GameObject helperGo)
    {
        var relay = FindRelayComponent(helperGo);
        if (relay == null) return null;
        var stateNames = RelayStringArray(relay, "m_stateNames");
        var targetNames = RelayStringArray(relay, "m_targetNames");
        if (stateNames == null || stateNames.Length == 0) return null;
        var entries = new List<WiringEntry>();
        for (int i = 0; i < stateNames.Length && i < targetNames.Length; i++)
        {
            var e = new WiringEntry { groupName = targetNames[i], idx = 0 };
            var sn = stateNames[i];
            if (sn == "ARun") e.side = "A";
            else if (sn == "BRun") e.side = "B";
            else if (sn == "Run")
            {
                // 同按模式：全部组挂在单一 Run 状态（多条目）。
            }
            else if (sn.StartsWith("Run_", StringComparison.Ordinal))
            {
                int v;
                int.TryParse(sn.Substring(4), out v);
                e.idx = v;
            }
            else continue;
            entries.Add(e);
        }
        return entries.Count > 0 ? entries : null;
    }

    private static Component FindRelayComponent(GameObject helperGo)
    {
        var relayType = LayoutEditorStubIO.FindCustomStubType(helperGo, "ButtonLogicRelay");
        return relayType != null ? helperGo.GetComponent(relayType) : null;
    }

    private static string[] RelayStringArray(Component relay, string field)
    {
        var f = relay.GetType().GetField(field, System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance);
        return f != null ? f.GetValue(relay) as string[] : null;
    }

    /// <summary>② 组根 TriggerQueue 接线读取：m_endTriggerTarget 指向 helper 的组，
    ///  按 m_startTrigger（BLGo_&lt;helper&gt;_[A|B]&lt;i&gt;）的尾标排序。纯场景数据，
    ///  不依赖 controller 资产（2026-09-24 SMB 回导恒空事故后的事实权威）。</summary>
    private static List<WiringEntry> ReadQueueWiring(string helperName, GameObject helperGo)
    {
        var animatedRoot = LayoutEditorHierarchy.FindByPath("Design/Animated Objects");
        if (animatedRoot == null) return null;
        var entries = new List<WiringEntry>();
        var prefix = "BLGo_" + helperName + "_";
        for (int i = 0; i < animatedRoot.childCount; i++)
        {
            var groupRoot = animatedRoot.GetChild(i);
            var q = groupRoot.GetComponent<TriggerQueue>();
            if (q == null || q.m_endTriggerTarget != helperGo) continue;
            var st = q.m_startTrigger;
            if (string.IsNullOrEmpty(st) || !st.StartsWith(prefix, StringComparison.Ordinal)) continue;
            var tail = st.Substring(prefix.Length);
            var e = new WiringEntry { groupName = groupRoot.name, idx = 0 };
            if (tail.Length > 0 && (tail[0] == 'A' || tail[0] == 'B'))
            {
                e.side = tail[0].ToString();
                tail = tail.Substring(1);
            }
            int v;
            int.TryParse(tail, out v);
            e.idx = v;
            entries.Add(e);
        }
        entries.Sort((a, b) =>
        {
            int c = string.CompareOrdinal(a.side, b.side);
            return c != 0 ? c : a.idx.CompareTo(b.idx);
        });
        return entries.Count > 0 ? entries : null;
    }

    /// <summary>③ controller Run 状态 SMB 读取（旧场景兜底）。</summary>
    private static List<WiringEntry> ReadSmbEntries(AnimatorStateMachine sm,
        Dictionary<string, AnimGroupDto> groupByStartTrigger, string runStatePrefix)
    {
        if (sm == null) return null;
        var entries = new List<WiringEntry>();
        foreach (var cs in sm.states)
        {
            var st = cs.state;
            if (st == null) continue;
            string side = null;
            int idx = 0;
            if (runStatePrefix == "ARun" || runStatePrefix == "BRun")
            {
                if (st.name != runStatePrefix) continue;
                side = runStatePrefix.Substring(0, 1);
            }
            else if (st.name.StartsWith("Run_", StringComparison.Ordinal))
            {
                if (!int.TryParse(st.name.Substring(4), out idx)) continue;
            }
            else continue;
            foreach (var trigger in ReadSendTriggers(st))
            {
                AnimGroupDto g;
                if (!groupByStartTrigger.TryGetValue(trigger, out g)) continue;
                entries.Add(new WiringEntry { side = side, idx = idx, groupName = g.displayName });
                break;
            }
        }
        if (entries.Count == 0) return null;
        entries.Sort((a, b) =>
        {
            int c = string.CompareOrdinal(a.side, b.side);
            return c != 0 ? c : a.idx.CompareTo(b.idx);
        });
        return entries;
    }

    /// <summary>三通道合一：任一通道给出接线即用（relay → 组根接线 → SMB）。</summary>
    private static List<WiringEntry> CollectEntries(string helperName, GameObject helperGo,
        AnimatorStateMachine sm, Dictionary<string, AnimGroupDto> groupByStartTrigger,
        string smbRunPrefix)
    {
        var entries = ReadRelayEntries(helperGo);
        if (entries != null && entries.Count > 0) return entries;
        entries = ReadQueueWiring(helperName, helperGo);
        if (entries != null && entries.Count > 0) return entries;
        return ReadSmbEntries(sm, groupByStartTrigger, smbRunPrefix);
    }

    private static void ImportSequence(List<LayoutButtonLinkDto> result, string helperName,
        GameObject helperGo, Animator anim, AnimatorStateMachine sm,
        Dictionary<string, AnimGroupDto> groupByStartTrigger, HashSet<string> groupNames,
        List<LayoutItemDto> items)
    {
        var sourceId = FindSourceId(items, anim, AdvanceTrigger(helperName));
        if (string.IsNullOrEmpty(sourceId))
        {
            LayoutEditorLog.LogWarning("button link: helper " + helperName + " 找不到触发源，跳过导入");
            return;
        }
        var shared = FindAllSourceIds(items, anim, AdvanceTrigger(helperName));
        shared.Remove(sourceId);

        var entries = CollectEntries(helperName, helperGo, sm, groupByStartTrigger, "Run_");
        if (entries == null || entries.Count == 0)
        {
            // 无绑定动画组：仍可回导共轭按钮配对（动画在触发编排中单独配置）。
            if (shared.Count == 0)
            {
                LayoutEditorLog.Log("button link: helper " + helperName +
                    " 无绑定组且无共轭按钮，跳过回导");
                return;
            }
            result.Add(new LayoutButtonLinkDto
            {
                id = ImportedSeqMarker + helperName,
                sourceId = sourceId,
                sharedSourceIds = shared.ToArray(),
                groupNames = new string[0],
                lockUntilFinished = helperName.StartsWith(HelperSeqPrefix, StringComparison.Ordinal),
            });
            return;
        }
        var names = new List<string>();
        foreach (var e in entries)
        {
            if (string.IsNullOrEmpty(e.groupName)) continue;
            if (!groupNames.Contains(e.groupName))
            {
                LayoutEditorLog.LogWarning("button link: helper " + helperName + " 接线的组「" +
                    e.groupName + "」已不存在，整条联动跳过导入");
                return;
            }
            names.Add(e.groupName);
        }
        if (names.Count == 0 && shared.Count == 0) return;

        // 同按模式识别：relay 有 ≥2 条 "Run" 条目（全组挂单一状态）；relay 缺失时
        // 退回控制器形状（存在 "Run" 且无 "Run_0" + 多组接线）。
        bool simultaneous = false;
        var relaySns = RelayStringArray(FindRelayComponent(helperGo), "m_stateNames");
        if (relaySns != null)
        {
            int runCount = 0;
            foreach (var sn in relaySns)
                if (sn == "Run") runCount++;
            simultaneous = runCount >= 2;
        }
        else if (sm != null && entries.Count >= 2)
        {
            bool hasRun = false, hasRun0 = false;
            foreach (var cs in sm.states)
            {
                if (cs.state == null) continue;
                if (cs.state.name == "Run") hasRun = true;
                if (cs.state.name == "Run_0") hasRun0 = true;
            }
            simultaneous = hasRun && !hasRun0;
        }

        string sequenceMode = null;
        if (!simultaneous)
        {
            // pingpong 烘焙会把中间步骤反向追加为 A-B-C-B。
            // 回导时压回唯一的正向半段，避免下一次写回产生重复动画组。
            for (int half = 2; half <= names.Count; half++)
            {
                if (names.Count != half * 2 - 2) continue;
                bool matches = true;
                for (int i = 1; i < half - 1; i++)
                {
                    if (names[half - 1 + i] != names[half - 1 - i])
                    {
                        matches = false;
                        break;
                    }
                }
                if (matches)
                {
                    names.RemoveRange(half, names.Count - half);
                    sequenceMode = "pingpong";
                    break;
                }
            }
        }

        // 共控按钮：其余接线到同一 helper（同触发名）的开关全部回导为 sharedSourceIds。
        result.Add(new LayoutButtonLinkDto
        {
            id = ImportedSeqMarker + helperName,
            sourceId = sourceId,
            sharedSourceIds = shared.Count > 0 ? shared.ToArray() : null,
            groupNames = names.ToArray(),
            sequenceMode = sequenceMode,
            simultaneous = simultaneous,
            lockUntilFinished = helperName.StartsWith(HelperSeqPrefix, StringComparison.Ordinal),
        });
    }

    private static void ImportPair(List<LayoutButtonLinkDto> result, string helperName,
        GameObject helperGo, Animator anim, AnimatorStateMachine sm,
        Dictionary<string, AnimGroupDto> groupByStartTrigger, HashSet<string> groupNames,
        List<LayoutItemDto> items)
    {
        var pairId = ImportedPairMarker + helperName;
        var srcA = FindSourceId(items, anim, PressTrigger(helperName, "A"));
        var srcB = FindSourceId(items, anim, PressTrigger(helperName, "B"));
        if (string.IsNullOrEmpty(srcA) || string.IsNullOrEmpty(srcB))
        {
            LayoutEditorLog.LogWarning("button link: 互锁 helper " + helperName + " 触发源不全，跳过导入");
            return;
        }

        var groupsA = new List<string>();
        var groupsB = new List<string>();
        var entries = CollectEntries(helperName, helperGo, sm, groupByStartTrigger, "ARun");
        if (entries != null)
        {
            foreach (var e in entries)
            {
                if (string.IsNullOrEmpty(e.groupName) || !groupNames.Contains(e.groupName)) continue;
                if (e.side == "A") groupsA.Add(e.groupName);
                else if (e.side == "B") groupsB.Add(e.groupName);
            }
        }

        bool aStartsUp = sm != null && sm.defaultState != null && sm.defaultState.name == "AReady";
        result.Add(new LayoutButtonLinkDto
        {
            id = pairId + ":A",
            sourceId = srcA,
            groupNames = groupsA.ToArray(),
            lockUntilFinished = true,
            pairId = pairId,
            pairStartsUp = aStartsUp,
        });
        result.Add(new LayoutButtonLinkDto
        {
            id = pairId + ":B",
            sourceId = srcB,
            groupNames = groupsB.ToArray(),
            lockUntilFinished = true,
            pairId = pairId,
            pairStartsUp = !aStartsUp,
        });
    }

    /// <summary>读取状态上所有 SendTriggerToObject 的 m_triggerToSend（私有字段走
    ///  SerializedObject；Unity 2017 无公开 behaviours API）。</summary>
    private static List<string> ReadSendTriggers(AnimatorState st)
    {
        var triggers = new List<string>();
        var so = new SerializedObject(st);
        var behaviours = so.FindProperty("m_Behaviours");
        if (behaviours == null || !behaviours.isArray) return triggers;
        for (int i = 0; i < behaviours.arraySize; i++)
        {
            var bref = behaviours.GetArrayElementAtIndex(i).objectReferenceValue;
            var send = bref as SendTriggerToObject;
            if (send == null) continue;
            var bso = new SerializedObject(send);
            var tp = bso.FindProperty("m_triggerToSend");
            if (tp != null && !string.IsNullOrEmpty(tp.stringValue))
                triggers.Add(tp.stringValue);
        }
        return triggers;
    }

    /// <summary>反查触发源：stub 的 animatorToTrigger 指向该 helper 且触发名匹配。 </summary>
    private static string FindSourceId(List<LayoutItemDto> items, Animator anim, string pressTrigger)
    {
        foreach (var id in FindAllSourceIds(items, anim, pressTrigger))
            return id;
        return null;
    }

    /// <summary>反查【全部】接线到该 helper（同触发名）的触发源（主源 + 共控按钮）。</summary>
    private static List<string> FindAllSourceIds(List<LayoutItemDto> items, Animator anim, string pressTrigger)
    {
        var found = new List<string>();
        foreach (var item in items ?? new List<LayoutItemDto>())
        {
            if (item == null || string.IsNullOrEmpty(item.instanceId)) continue;
            var go = ResolveObject(item.instanceId, null);
            if (go == null) continue;
            var sw = go.GetComponent<PseudoPrefabSwitchStub>();
            if (sw != null && sw.animatorToTrigger == anim && sw.triggerOnAnimator == pressTrigger)
            {
                found.Add(item.instanceId);
                continue;
            }
            var toggleSw = go.GetComponent<PseudoPrefabToggleSwitchStub>();
            if (toggleSw != null && toggleSw.animatorToTrigger == anim && toggleSw.triggerOnAnimator == pressTrigger)
            {
                found.Add(item.instanceId);
                continue;
            }
            var ps = go.GetComponent<PseudoPrefabPressureSwitchStub>();
            if (ps != null && ps.animatorToTrigger == anim && ps.triggerOnAnimatorEnter == pressTrigger)
                found.Add(item.instanceId);
        }
        return found;
    }
}
