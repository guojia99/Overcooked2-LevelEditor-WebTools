using System;
using System.Collections.Generic;
using UnityEngine;

namespace CustomStub
{
    /// <summary>
    /// 订单顺序状态机（4.0.0.18（原 4.7.0））：接管 RoundData.GetNextRecipe 的出单序列。
    ///
    /// 配置来源：场景 OrderSequenceSettings 组件（EntryPoint.ProcessScene 在
    /// HealScene/清场完成后调 ActivateFromScene 读取；无组件 = 不激活零介入）。
    /// Entry 解析：uID → 关卡 RecipeList（RoundData.m_recipes）条目，按
    /// OrderDefinitionNode.m_uID 建映射（懒建、per RoundData 实例缓存、场景
    /// 切换清空）——与真机 OC2DIYLevel / 编辑器 LevelConfigSetup 两条 Entry
    /// 构建链同源（同一菜谱资产 uID 必然一致），运行时零资产加载。
    ///
    /// 状态机（见 OrderSequenceSettings 类头语义）：
    ///   组序列 gi 按 dir 前进；ordered 组指针 cursor 随 dir 走、越界组完成；
    ///   random 组激活时从去重池抽一批（≥N 无放回 / &lt;N 有放回补足），批内跑
    ///   原版防重复公式，出满 N 单组完成；组完成 AdvanceGroup——loop 回首、
    ///   pingpong 翻转 dir（钟摆，单组序列钉在 gi=0）、stop 停单（空数组）。
    /// 查不到的 uID（菜谱未选/debugRecipeCount 截断）跳过该单继续推进，防死循环
    /// 有 256 次守护上限，超限放行原逻辑（原版随机）。
    ///
    /// 调用方仅服务端出单路径（ServerOrderControllerBase.AddNewOrder），客户端
    /// 经 vanilla 网络序列化收单，无同步负担；状态为静态缓存，场景切换
    /// （OnSceneChanged，随 EntryPoint.ResetSceneTickers 链）全清。
    /// </summary>
    internal static class OrderSequenceDirector
    {
        private const string Version = "v1(" + StubVersion.Value + ")";

        // ---- 配置（ActivateFromScene 解析产物） ----
        private static bool s_active;
        private static string s_end = "loop";
        private static int s_groupCount;
        private static int[] s_modes;      // 0=ordered 1=random
        private static int[][] s_uids;     // 每组 uID 列表
        private static int[] s_batches;    // random 抽菜数（0=整池）

        // ---- 运行态 ----
        private static int s_gi;
        private static int s_dir = 1;
        private static bool s_stopped;
        private static bool s_emptyGroup;
        private static int s_cursor;           // ordered 组指针
        private static int[] s_batchUid;       // random 当前批槽位 uID
        private static int[] s_batchCounts;    // 槽位已出次数
        private static int s_served;           // 批内已出单数

        // ---- uID → Entry 映射（per RoundData 实例懒建；OnSceneChanged 清） ----
        private static readonly Dictionary<object, Dictionary<int, object>> s_entryMaps =
            new Dictionary<object, Dictionary<int, object>>();

        private static Array s_emptyEntries;
        private static bool s_missingUidWarned;

        /// <summary>前缀快速放行门：场景无配置零开销。</summary>
        internal static bool Active
        {
            get { return s_active; }
        }

        // ------------------------------------------------ 场景生命周期

        /// <summary>场景自愈完成后由 EntryPoint 调用：读场景组件激活配置。
        /// 幂等（先清后建）；组件缺失/配置无效 = 不激活（原版随机出单）。</summary>
        internal static void ActivateFromScene()
        {
            Deactivate();
            try
            {
                var settings = UnityEngine.Object.FindObjectOfType<OrderSequenceSettings>();
                if (settings == null)
                    return;

                var sizes = settings.m_groupSizes;
                var uids = settings.m_recipeUids;
                if (sizes == null || uids == null || sizes.Length == 0)
                {
                    StubLog.LogWarn("[CustomStub] 订单顺序配置无效（组列表为空），维持原版随机出单");
                    return;
                }

                // 切组 + 过滤空组（sizes 总和与 uids 长度不符时截断）
                var keptIdx = new List<int>();
                var groups = new List<int[]>();
                int offset = 0;
                for (int i = 0; i < sizes.Length; i++)
                {
                    int len = sizes[i];
                    if (len <= 0)
                        continue; // 空组不切分
                    if (offset + len > uids.Length)
                        break; // 数据不完整，截断（烘焙方保证不发生）
                    var group = new int[len];
                    Array.Copy(uids, offset, group, 0, len);
                    offset += len;
                    groups.Add(group);
                    keptIdx.Add(i);
                }
                if (groups.Count == 0)
                {
                    StubLog.LogWarn("[CustomStub] 订单顺序配置无效（无有效组），维持原版随机出单");
                    return;
                }

                s_groupCount = groups.Count;
                s_uids = groups.ToArray();
                s_modes = new int[s_groupCount];
                s_batches = new int[s_groupCount];
                for (int k = 0; k < s_groupCount; k++)
                {
                    int i = keptIdx[k];
                    s_modes[k] = settings.m_groupModes != null && i < settings.m_groupModes.Length
                        ? settings.m_groupModes[i] : 0;
                    s_batches[k] = settings.m_groupBatches != null && i < settings.m_groupBatches.Length
                        ? settings.m_groupBatches[i] : 0;
                }

                s_end = settings.m_endBehavior;
                if (s_end != "loop" && s_end != "pingpong" && s_end != "stop")
                    s_end = "loop";

                s_gi = 0;
                s_dir = 1;
                s_stopped = false;
                s_missingUidWarned = false;
                InitGroup();
                s_active = true;
                EntryPoint.EnsureOrderSequencePatches();
                StubLog.Log("[CustomStub] 订单顺序接管 " + Version + ": " + s_groupCount
                    + " 组，结束行为=" + s_end);
            }
            catch (Exception ex)
            {
                Deactivate();
                StubLog.LogWarn("[CustomStub] 订单顺序配置解析失败（维持原版随机出单）: " + ex.Message);
            }
        }

        /// <summary>全清（场景切换 / ProcessScene 未命中分支）。</summary>
        internal static void OnSceneChanged()
        {
            Deactivate();
        }

        private static void Deactivate()
        {
            s_active = false;
            s_stopped = false;
            s_emptyGroup = false;
            s_groupCount = 0;
            s_gi = 0;
            s_dir = 1;
            s_uids = null;
            s_modes = null;
            s_batches = null;
            s_batchUid = null;
            s_batchCounts = null;
            s_served = 0;
            s_cursor = 0;
            s_entryMaps.Clear();
        }

        // ------------------------------------------------ 出单（prefix 调用）

        /// <summary>取下一单。返回 true = 接管（entries 为 Entry[1] 或空数组=停单）；
        /// false = 放行原方法（未激活/状态机异常兜底）。</summary>
        internal static bool TryPick(object roundData, out object entries)
        {
            entries = null;
            if (!s_active)
                return false;
            if (GameApi.RecipeListEntryType == null)
                return false;

            var map = EntryMapFor(roundData);
            if (map == null || map.Count == 0)
            {
                WarnOnceNoMap();
                return false;
            }

            int guard = 0;
            while (guard++ < 256)
            {
                if (s_stopped)
                {
                    entries = EmptyEntries();
                    return true;
                }
                if (s_emptyGroup || s_gi < 0 || s_gi >= s_groupCount)
                {
                    AdvanceGroup();
                    continue;
                }

                int uid;
                bool groupDone;
                if (s_modes[s_gi] == 1)
                {
                    int n = s_batchUid != null ? s_batchUid.Length : 0;
                    if (n <= 0)
                    {
                        s_emptyGroup = true;
                        continue;
                    }
                    int slot = WeightedPick(n);
                    uid = s_batchUid[slot];
                    s_batchCounts[slot]++;
                    s_served++;
                    groupDone = s_served >= n;
                }
                else
                {
                    int[] g = s_uids[s_gi];
                    if (g.Length == 0)
                    {
                        s_emptyGroup = true;
                        continue;
                    }
                    uid = g[s_cursor];
                    s_cursor += s_dir;
                    groupDone = s_cursor < 0 || s_cursor >= g.Length;
                }

                object entry;
                if (!map.TryGetValue(uid, out entry))
                {
                    if (!s_missingUidWarned)
                    {
                        s_missingUidWarned = true;
                        StubLog.LogWarn("[CustomStub] 订单顺序: uID " + uid
                            + " 不在本关菜谱列表（未选/被调试菜谱数截断），该单跳过");
                    }
                    if (groupDone)
                        AdvanceGroup();
                    continue; // 跳过本单，继续推进状态机
                }

                var arr = Array.CreateInstance(GameApi.RecipeListEntryType, 1);
                arr.SetValue(entry, 0);
                entries = arr;
                if (groupDone)
                    AdvanceGroup();
                return true;
            }

            StubLog.LogWarn("[CustomStub] 订单顺序状态机守护上限触发（疑似全组配置无效），本单放行原逻辑");
            return false;
        }

        // ------------------------------------------------ 组推进

        private static void InitGroup()
        {
            s_emptyGroup = false;
            var g = s_uids[s_gi];
            if (s_modes[s_gi] == 1)
            {
                // 去重池
                var pool = new List<int>();
                for (int i = 0; i < g.Length; i++)
                {
                    if (!pool.Contains(g[i]))
                        pool.Add(g[i]);
                }
                if (pool.Count == 0)
                {
                    s_emptyGroup = true;
                    return;
                }
                int n = s_batches[s_gi] > 0 ? s_batches[s_gi] : pool.Count;
                // 抽批：洗牌池；池 < N 有放回重复补足（10 抽 15 = 15 条含重复）
                var shuffled = ShuffleCopy(pool);
                s_batchUid = new int[n];
                for (int i = 0; i < n; i++)
                    s_batchUid[i] = shuffled[i % shuffled.Count];
                s_batchCounts = new int[n];
                s_served = 0;
            }
            else
            {
                if (g.Length == 0)
                {
                    s_emptyGroup = true;
                    return;
                }
                s_cursor = s_dir > 0 ? 0 : g.Length - 1;
            }
        }

        private static void AdvanceGroup()
        {
            if (s_groupCount <= 0)
            {
                s_stopped = true;
                return;
            }
            int next = s_gi + s_dir;
            if (next < 0 || next >= s_groupCount)
            {
                if (s_end == "pingpong")
                {
                    s_dir = -s_dir;
                    next = s_gi + s_dir;
                    if (next < 0 || next >= s_groupCount)
                        next = s_gi; // 单组序列：钉在本组重入（重抽/重放）
                }
                else if (s_end == "loop")
                {
                    s_dir = 1;
                    next = 0;
                }
                else
                {
                    s_stopped = true;
                    return;
                }
            }
            s_gi = next;
            InitGroup();
        }

        // ------------------------------------------------ 原版公式镜像

        /// <summary>原版防重复加权随机（RoundData.GetWeight + ArrayUtils.
        /// GetWeightedRandomElement 镜像）：weight_i = max((served+2)/N − cnt_i, 0)，
        /// 轮盘赌 UnityEngine.Random（与原版同源）。浮点边界兜底返回末个正权重槽位
        /// （原版此处返回 -1 会数组越界，属原版不可达边界）。</summary>
        private static int WeightedPick(int n)
        {
            float avg = (s_served + 2f) / n;
            float total = 0f;
            for (int i = 0; i < n; i++)
            {
                float w = avg - s_batchCounts[i];
                if (w > 0f)
                    total += w;
            }
            float r = UnityEngine.Random.Range(0f, total);
            float acc = 0f;
            int lastPositive = 0;
            for (int i = 0; i < n; i++)
            {
                float w = avg - s_batchCounts[i];
                if (w > 0f)
                {
                    lastPositive = i;
                    acc += w;
                    if (r <= acc)
                        return i;
                }
            }
            return lastPositive;
        }

        private static List<int> ShuffleCopy(List<int> src)
        {
            var list = new List<int>(src);
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                int tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
            return list;
        }

        // ------------------------------------------------ Entry 解析

        private static Dictionary<int, object> EntryMapFor(object roundData)
        {
            if (roundData == null)
                return null;
            Dictionary<int, object> map;
            if (s_entryMaps.TryGetValue(roundData, out map))
                return map;
            if (GameApi.RoundDataRecipesField == null || GameApi.RecipeListRecipesField == null
                || GameApi.EntryOrderField == null || GameApi.NodeUidField == null)
                return null;
            try
            {
                map = new Dictionary<int, object>();
                var list = GameApi.RoundDataRecipesField.GetValue(roundData);
                var arr = list == null ? null : GameApi.RecipeListRecipesField.GetValue(list) as Array;
                if (arr != null)
                {
                    foreach (object e in arr)
                    {
                        if (e == null)
                            continue;
                        var node = GameApi.EntryOrderField.GetValue(e);
                        if (node == null)
                            continue;
                        object uidBox = GameApi.NodeUidField.GetValue(node);
                        if (uidBox == null)
                            continue;
                        int uid = (int)uidBox;
                        // uID 冲突（不同菜谱同 uID）后者覆盖前者：语义退化为同菜谱，罕见
                        map[uid] = e;
                    }
                }
                if (s_entryMaps.Count > 4)
                    s_entryMaps.Clear(); // 防场景残留实例累积
                s_entryMaps[roundData] = map;
                return map;
            }
            catch (Exception ex)
            {
                StubLog.LogWarn("[CustomStub] 订单顺序 Entry 映射构建失败: " + ex.Message);
                return null;
            }
        }

        private static object EmptyEntries()
        {
            if (s_emptyEntries == null || s_emptyEntries.Length != 0)
                s_emptyEntries = Array.CreateInstance(GameApi.RecipeListEntryType, 0);
            return s_emptyEntries;
        }

        private static bool s_noMapWarned;

        private static void WarnOnceNoMap()
        {
            if (s_noMapWarned)
                return;
            s_noMapWarned = true;
            StubLog.LogWarn("[CustomStub] 订单顺序: 本关 RecipeList 为空/反射缺失，放行原逻辑");
        }
    }
}
