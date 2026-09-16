using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CustomStub
{
    /// <summary>
    /// 联机同步诊断（2026-09-15 v14，可移动火锅联机 ID 错位事故的直接产物）。
    ///
    /// 【为什么需要它】游戏的网络实体 ID 是
    /// <c>EntitySerialisationRegistry</c> 按【扫描命中顺序】发放的自增 FIFO
    /// （EntitySerialisationRegistry.cs:123-129 填 1..1022、:439-442 Dequeue），
    /// 与对象身份无关。主客机各自本地跑一遍
    /// <c>LinkAllEntitiesToSynchronisationScripts</c>，靠「两边遍历出的对象序列
    /// 完全一致」来保证 ID 对齐。一旦某台机器在扫描窗口期多/少了一个对象，
    /// 从该点起所有实体 ID 整体错位——症状是客机完全无法操作、双方厨师原地不动、
    /// 生成物没有模型，而日志里【没有任何报错】。
    ///
    /// 本模块把这条此前完全不可观测的链路变成两行常开日志：
    ///  - <see cref="OnScanEntitiesBegin"/>：扫描发令时刻的本机快照（角色/场景/
    ///    待装配 stub 对象是否都已就位）；
    ///  - <see cref="OnStartSynchronisation"/>：扫描结果的【实体指纹】——
    ///    主客机两行一比即可判定 ID 是否对齐；不一致时还有分段指纹指出
    ///    从第几个实体开始分叉。
    ///
    /// 排障口诀：**指纹相同 = 实体层对齐（问题在别处）；指纹不同 = ID 错位，
    /// 看分段指纹第一个不同的段，再看该段附近是谁多/少了对象。**
    /// </summary>
    internal static class NetDiagnostics
    {
        /// <summary>分段指纹的段长（每 N 个实体一段，用于定位分叉起点）。</summary>
        private const int SegmentSize = 100;

        /// <summary>每场景只报一次（StartSynchronisation 每关一次，但防御重入）。</summary>
        private static bool s_reportedThisScene;

        /// <summary>本场景的网络同步是否已启动（= 实体扫描已彻底结束）。
        /// PushablePot 用它判定「晚到装配」——不能用
        /// MultiplayerController.IsSynchronisationActive()，那是跨关卡不复位的
        /// 静态标志（上一关留下的 true 会让每张新图都误报晚到）。</summary>
        internal static bool SyncStartedThisScene
        {
            get { return s_reportedThisScene; }
        }

        internal static void OnSceneChanged()
        {
            s_reportedThisScene = false;
            s_scanCompletedLogged = false;
            s_entitiesStartedLogged = false;
            s_scanBeginTime = 0f;
        }

        /// <summary>关卡网络时序打点的去重标志（ScannedEntities 会被
        /// ClientKitchenLoader.Update 每帧重复调用直到状态迁移，必须限一次）。</summary>
        private static bool s_scanCompletedLogged;
        private static bool s_entitiesStartedLogged;
        private static float s_scanBeginTime;

        /// <summary>ClientKitchenLoader.ScannedEntities 前缀：扫描+组件缓存都已完成。
        /// 主客机对比这一行的耗时，能直接看出谁的扫描窗口更长/更晚。</summary>
        internal static void OnScanCompleted()
        {
            if (s_scanCompletedLogged)
                return;
            s_scanCompletedLogged = true;
            try
            {
                var dict = GameApi.GetEntitiesByGameObject();
                var cost = s_scanBeginTime > 0f ? Time.realtimeSinceStartup - s_scanBeginTime : -1f;
                StubLog.Log("[Net] 实体扫描完成: 角色=" + GameApi.RoleLabel()
                    + " 实体=" + (dict != null ? dict.Count.ToString() : "?") + " 个"
                    + (cost >= 0f ? " 耗时=" + cost.ToString("0.000") + "s" : ""));
            }
            catch (Exception ex)
            {
                StubLog.LogWarn("[Net] 扫描完成日志异常（忽略）: " + ex.Message);
            }
        }

        /// <summary>ClientKitchenLoader.StartEntities 前缀：所有实体收到首包，关卡真正开跑。</summary>
        internal static void OnEntitiesStarted()
        {
            if (s_entitiesStartedLogged)
                return;
            s_entitiesStartedLogged = true;
            try
            {
                StubLog.Log("[Net] 实体启动完成: 角色=" + GameApi.RoleLabel()
                    + " 联机=" + (GameApi.IsInSession() ? "是" : "否") + "（关卡开始）");
            }
            catch (Exception ex)
            {
                StubLog.LogWarn("[Net] 实体启动日志异常（忽略）: " + ex.Message);
            }
        }

        /// <summary>ScanEntities 前缀（PushablePot 预装配之后）调用：记录发令时刻快照。
        /// 这条日志的价值是「扫描开始时本机场景里到底有没有那两口锅」——
        /// 主客机对比即可发现装配时机差异。</summary>
        internal static void OnScanEntitiesBegin(int potTotal, int potAssembled)
        {
            try
            {
                s_scanBeginTime = Time.realtimeSinceStartup;
                StubLog.Log("[Net] 实体扫描开始: 场景=" + SceneManager.GetActiveScene().name
                    + " 角色=" + GameApi.RoleLabel()
                    + " 可移动火锅=" + potAssembled + "/" + potTotal + " 已装配"
                    + (potAssembled < potTotal ? "（⚠ 有锅未就位，两机层级可能不一致）" : ""));
            }
            catch (Exception ex)
            {
                StubLog.LogWarn("[Net] 扫描开始日志异常（忽略）: " + ex.Message);
            }
        }

        /// <summary>StartSynchronisation 前缀调用：实体指纹 + 逐锅注册自检。
        /// 此时链接循环已结束、实体表已定型，但 StartSynchronisingEntry 还没跑，
        /// 正是「扫描结果」的纯净快照。</summary>
        internal static void OnStartSynchronisation()
        {
            if (s_reportedThisScene)
                return;
            s_reportedThisScene = true;
            try
            {
                var entries = CollectEntities();
                if (entries == null)
                {
                    StubLog.LogWarn("[Net] 实体注册表反射缺失，无法输出实体指纹（联机 ID 对齐无法自证）");
                }
                else
                {
                    StubLog.Log("[Net] 实体扫描结果: 场景=" + SceneManager.GetActiveScene().name
                        + " 角色=" + GameApi.RoleLabel()
                        + " 实体=" + entries.Count + " 个"
                        + " 指纹=" + Fingerprint(entries)
                        + "（联机时【实体数与指纹】必须与对端完全相同，不同 = 实体 ID 错位）");
                    StubLog.Log("[Net] 分段指纹(每" + SegmentSize + "个): " + SegmentFingerprints(entries));
                    if (StubLog.Verbose)
                        DumpAll(entries);
                }
                PushablePot.DumpRegistrationSelfCheck();
            }
            catch (Exception ex)
            {
                StubLog.LogWarn("[Net] 实体指纹输出异常（忽略，不影响玩法）: " + ex.Message);
            }
        }

        /// <summary>按实体 ID 升序收集 "id:对象名"。ID 相同不可能（字典键唯一），
        /// 但仍按名字二次排序保证完全确定。</summary>
        private static List<string> CollectEntities()
        {
            var dict = GameApi.GetEntitiesByGameObject();
            if (dict == null)
                return null;
            var pairs = new List<KeyValuePair<uint, string>>(dict.Count);
            foreach (System.Collections.DictionaryEntry kv in dict)
            {
                var go = kv.Key as GameObject;
                if (go == null)
                    continue;
                pairs.Add(new KeyValuePair<uint, string>(GameApi.GetEntityId(go), go.name));
            }
            pairs.Sort(delegate(KeyValuePair<uint, string> a, KeyValuePair<uint, string> b)
            {
                if (a.Key != b.Key)
                    return a.Key < b.Key ? -1 : 1;
                return string.CompareOrdinal(a.Value, b.Value);
            });
            var list = new List<string>(pairs.Count);
            for (int i = 0; i < pairs.Count; i++)
                list.Add(pairs[i].Key + ":" + pairs[i].Value);
            return list;
        }

        private static string Fingerprint(List<string> entries)
        {
            uint h = 2166136261u;
            for (int i = 0; i < entries.Count; i++)
                h = Fnv1a(entries[i], h);
            return h.ToString("X8");
        }

        /// <summary>分段指纹：每 SegmentSize 个实体一个哈希。主客机逐段对比，
        /// 第一个不同的段就是「两边序列开始分叉」的位置。</summary>
        private static string SegmentFingerprints(List<string> entries)
        {
            var sb = new System.Text.StringBuilder();
            uint h = 2166136261u;
            var seg = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                h = Fnv1a(entries[i], h);
                if ((i + 1) % SegmentSize == 0)
                {
                    if (seg++ > 0)
                        sb.Append(' ');
                    sb.Append(i + 2 - SegmentSize).Append('-').Append(i + 1).Append('=').Append(h.ToString("X8"));
                    h = 2166136261u;
                }
            }
            var rest = entries.Count % SegmentSize;
            if (rest != 0)
            {
                if (seg > 0)
                    sb.Append(' ');
                sb.Append(entries.Count - rest + 1).Append('-').Append(entries.Count)
                    .Append('=').Append(h.ToString("X8"));
            }
            return sb.Length == 0 ? "(空)" : sb.ToString();
        }

        /// <summary>Verbose 下的全量清单（每行 20 个，便于两机日志逐行 diff）。</summary>
        private static void DumpAll(List<string> entries)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < entries.Count; i++)
            {
                if (i % 20 != 0)
                    sb.Append(" | ");
                sb.Append(entries[i]);
                if ((i + 1) % 20 == 0 || i == entries.Count - 1)
                {
                    StubLog.Dbg("[Net] 实体清单 " + (i / 20 * 20 + 1) + "+: " + sb);
                    sb.Length = 0;
                }
            }
        }

        private static uint Fnv1a(string s, uint seed)
        {
            unchecked
            {
                var h = seed;
                for (int i = 0; i < s.Length; i++)
                {
                    h ^= s[i];
                    h *= 16777619u;
                }
                return h;
            }
        }
    }
}
