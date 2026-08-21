using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 火锅灶台（CookingRegion / floorburner）Play 期诊断：
/// 定位「灶台不点火、锅不加热、汤面显示异常」链路中哪一环断了。
///
/// 每 0.5s 对场景里所有 CookingRegion 输出：
///  - region.enabled / TriggerArea.enabled / StationType
///  - TriggerRecorder 命中的 collider 数（锅是否进入触发区）
///  - 命中对象里 ICookable/IClientCookable 的 GetRequiredStationType（类型是否匹配）
///  - region 所在格子 vs 锅所在格子（ServerCookingRegion 的同格加热条件）
///  - 锅内容物 GetPrefabForNode 结果（汤面 lookup 是否命中）
/// 只读诊断，不修改任何运行时对象。仅编辑器 Play 模式生效。
/// </summary>
[InitializeOnLoad]
public static class LayoutEditorHotPotDiagnostics
{
    private static double s_nextTick;
    private static bool s_loggedOnce;

    static LayoutEditorHotPotDiagnostics()
    {
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (!Application.isPlaying || EditorApplication.isPaused)
            return;
        if (EditorApplication.timeSinceStartup < s_nextTick)
            return;
        s_nextTick = EditorApplication.timeSinceStartup + 0.5;

        var regions = Object.FindObjectsOfType<CookingRegion>();
        if (regions.Length == 0)
        {
            if (!s_loggedOnce)
            {
                Debug.Log("[HotPotDiag] 场景中没有任何 CookingRegion（floorburner 未生效？）");
                s_loggedOnce = true;
            }
            return;
        }
        s_loggedOnce = false;
        foreach (var region in regions)
        {
            Dump(region);
        }
    }

    private static void Dump(CookingRegion region)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("[HotPotDiag] region=").Append(region.name)
          .Append(" enabled=").Append(region.enabled)
          .Append(" stationType=").Append((int)region.m_StationType)
          .Append(" triggerArea=").Append(region.m_TriggerArea != null && region.m_TriggerArea.enabled);
        if (region.m_TriggerArea != null)
        {
            var b = region.m_TriggerArea.bounds;
            sb.Append(" area=").Append(b.ToString("F2"));
        }

        var recorder = region.GetComponent<TriggerRecorder>();
        int collisions = 0;
        if (recorder != null)
        {
            var list = recorder.GetRecentCollisions();
            collisions = list != null ? list.Count : 0;
            sb.Append(" collisions=").Append(collisions);
        }
        // 灶火状态：每个火焰粒子系统是否在播、位置（定位 ClientCookingRegion 点火链路）。
        if (region.m_flameEffects != null)
        {
            int playing = 0;
            foreach (var pfx in region.m_flameEffects)
            {
                if (pfx == null) continue;
                if (pfx.isPlaying) playing++;
            }
            sb.Append(" flames=").Append(playing).Append("/").Append(region.m_flameEffects.Length);
            if (region.m_flameEffects.Length > 0 && region.m_flameEffects[0] != null)
                sb.Append(" flamePos0=").Append(region.m_flameEffects[0].transform.position.ToString("F2"));
            sb.Append(" glow=").Append(region.m_glowEffect != null && region.m_glowEffect.isPlaying);
        }
        if (recorder != null)
        {
            var list = recorder.GetRecentCollisions();
            if (list != null)
            {
                foreach (var col in list)
                {
                    if (col == null) continue;
                    var cookable = col.gameObject.RequestInterface<ICookable>();
                    var baseCookable = col.gameObject.RequestInterface<IBaseCookable>();
                    sb.Append("\n  hit: ").Append(col.name)
                      .Append(" ICookable=").Append(cookable != null)
                      .Append(" reqType=").Append(baseCookable != null ? ((int)baseCookable.GetRequiredStationType()).ToString() : "n/a");
                    if (cookable != null)
                    {
                        try
                        {
                            var iorder = col.gameObject.RequestInterface<IOrderDefinition>();
                            var comp = iorder != null ? iorder.GetOrderComposition() : null;
                            var simple = comp != null ? comp.Simpilfy() : null;
                            sb.Append(" contents=").Append(simple != null ? simple.ToString() : "null");
                        }
                        catch (System.NullReferenceException)
                        {
                            // 锅刚实例化/未同步完成时 GetOrderComposition 可能 NRE，跳过本次。
                        }
                    }
                }
            }
        }
        else
        {
            sb.Append(" recorder=MISSING");
        }

        var gridManager = GameUtils.GetGridManager(region.transform);
        if (gridManager != null)
        {
            sb.Append("\n  regionGrid(pos)=").Append(gridManager.GetGridLocationFromPos(region.transform.position));
        }
        // 真正参与「锅灶同格」判断的是同步器缓存的 m_gridIndex（CookingRegionPatch 覆写对象）。
        var server = region.GetComponent<ServerCookingRegion>();
        if (server != null)
        {
            var f = typeof(ServerCookingRegion).GetField("m_gridIndex",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            sb.Append("\n  serverCachedGrid=").Append(f != null ? f.GetValue(server) : "?");
        }
        if (recorder != null)
        {
            var list = recorder.GetRecentCollisions();
            if (list != null)
            {
                foreach (var col in list)
                {
                    if (col == null) continue;
                    if (col.gameObject.RequestInterface<ICookable>() == null) continue;
                    var gm2 = GameUtils.GetGridManager(col.transform);
                    var potIdx = gm2 != null ? gm2.GetGridLocationFromPos(col.transform.position).ToString() : "?";
                    sb.Append("\n  potGrid=").Append(potIdx);
                }
            }
        }
        Debug.Log(sb.ToString());
    }
}
