using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// 火锅大锅/灶台运行时管理器（随游戏编译，编辑器与打包后都生效）。
///
/// 背景：编辑器摆放大锅（utensil_large_pot_01 / utensil_dlc10_large_pot_01）与
/// floorburner 灶台后，原版运行链路有四处断点：
///  1) 灶台 stub 在地板块层（grid y=0），锅在操作面层（y=1）：宿主 Server/Client
///     CookingRegion 缓存的 m_gridIndex 要求「锅灶同格」才加热/点火 → 永远不加热；
///  2) 锅底 wok_flame 环形火由 ClientWokEffectsCosmeticDecisions 驱动，只在
///     「锅占据 CookingRegion 格子」时点亮 → 同上易断；
///  3) 宿主 PseudoPrefabCookingUtensil.Setup 把 ContentsCosmeticDecisions.
///     m_contentsYPositionWhenEmpty 覆写成 -0.2（为煎锅设计），大锅原版 0.25
///     （WhenFull=0.4）→ 1 根面条汤面沉底不可见；
///  4) 煮熟「嘀嘀」提示依赖 UI 状态机链路，易断。
///
/// 本管理器每帧（轻量幂等）处理以上四项：抬格层、常燃 wok_flame、恢复汤面高度并
/// 按内容数即时重摆、监听煮熟翻转补发 ImCooked 提示音。只动内存/运行时对象，
/// 不改场景序列化，不改宿主文件。
/// </summary>
public static class LayoutRuntimeHotPot
{
    private static FieldInfo s_serverGridField;
    private static FieldInfo s_clientGridField;
    private static readonly Dictionary<ServerCookingHandler, bool> s_cookedState =
        new Dictionary<ServerCookingHandler, bool>();
    private static readonly HashSet<ContentsCosmeticDecisions> s_soupFixed =
        new HashSet<ContentsCosmeticDecisions>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        var go = new GameObject("LayoutRuntimeHotPot");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<HotPotTicker>();
    }

    private class HotPotTicker : MonoBehaviour
    {
        private void Update()
        {
            // 烹饪进度需要逐帧累积
            LayoutRuntimeHotPot.CookPotsOverBurner(Time.deltaTime);
            if (Time.frameCount % 10 != 0) // 其余修复 6Hz 足够，省开销
                return;
            LayoutRuntimeHotPot.Tick();
        }
    }

    private static void Tick()
    {
        LiftCookingRegionGrid();
        KeepWokFlameOn();
        FixSoupLevel();
        AlertWhenCooked();
    }

    
    
    private static FieldInfo s_pilotGridTargetField;

    /// <summary>可移动火锅灶台吸附：锅（载具）碰撞中心进入灶台触发区后，把宿主
    ///  ServerPilotMovement 的吸附目标 m_gridTarget 改写为「碰撞中心=触发区中心」
    ///  的位置（碰撞中心恒偏载具原点 (-0.6,+0.6)），宿主自己平滑移过去——
    ///  不直接改 transform，不跟宿主抢速度，无抽搐。玩家按住拖动时宿主会清掉
    ///  m_gridTarget（Update_Movement 每帧置 null），只在松手吸附时生效。</summary>
    private static void SnapPushablePotToBurner()
    {
        if (s_pilotGridTargetField == null)
            s_pilotGridTargetField = typeof(ServerPilotMovement).GetField("m_gridTarget",
                BindingFlags.Instance | BindingFlags.NonPublic);
        if (s_pilotGridTargetField == null)
            return;
        foreach (var handler in Object.FindObjectsOfType<ServerPilotMovement>())
        {
            if (handler == null || !IsLargePot(handler.transform))
                continue;
            var col = handler.GetComponentInChildren<Collider>();
            if (col == null)
                continue;
            var center = col.bounds.center;
            var offset = handler.transform.position - center; // 碰撞中心→原点的偏移
            foreach (var region in Object.FindObjectsOfType<CookingRegion>())
            {
                if (region == null || !region.enabled || region.m_TriggerArea == null)
                    continue;
                var b = region.m_TriggerArea.bounds;
                if (center.x < b.min.x || center.x > b.max.x || center.z < b.min.z || center.z > b.max.z)
                    continue;
                var target = new Vector3(b.center.x, handler.transform.position.y, b.center.z) + offset;
                s_pilotGridTargetField.SetValue(handler, target);
                break;
            }
        }
    }

    /// <summary>直接驱动烹饪：宿主 ServerCookingRegion 要求「锅与灶同格（含缓存格层）」
    ///  才 Cook，可移动火锅（PushableObject 动态格子）常被挡在此链路外。
    ///  此处按「锅在启用的 CookingRegion 触发区内且有内容物」直接推进
    ///  ServerCookingHandler 烹饪进度（与宿主 Cook(deltaTime) 等价，幂等，
    ///  宿主链路若同时生效只会稍快，不影响正确性）。</summary>
    private static void CookPotsOverBurner(float deltaTime)
    {
        if (deltaTime <= 0f)
            return;
        foreach (var handler in Object.FindObjectsOfType<ServerCookingHandler>())
        {
            if (handler == null || !IsLargePot(handler.transform))
                continue;
            if (!IsOverBurner(handler.transform))
                continue;
            if (handler.IsBurning() || handler.IsCooked())
                continue;
            // 锅内无内容物不加热（与宿主 ServerCookingRegion 行为一致）
            var iorder = handler.gameObject.RequestInterface<IOrderDefinition>();
            var comp = iorder != null ? iorder.GetOrderComposition() : null;
            var simple = comp != null ? comp.Simpilfy() : null;
            if (simple == null || simple == AssembledDefinitionNode.NullNode)
                continue;
            handler.Cook(deltaTime);
        }
    }

        /// <summary>1) 把每个 CookingRegion 的 Server/Client 同步器缓存格层抬到 y=1（操作面）。</summary>
    private static void LiftCookingRegionGrid()
    {
        if (s_serverGridField == null)
            s_serverGridField = typeof(ServerCookingRegion).GetField("m_gridIndex",
                BindingFlags.Instance | BindingFlags.NonPublic);
        if (s_clientGridField == null)
            s_clientGridField = typeof(ClientCookingRegion).GetField("m_gridIndex",
                BindingFlags.Instance | BindingFlags.NonPublic);
        if (s_serverGridField == null || s_clientGridField == null)
            return;
        foreach (var server in Object.FindObjectsOfType<ServerCookingRegion>())
            LiftIndexToPlayLevel(server, s_serverGridField);
        foreach (var client in Object.FindObjectsOfType<ClientCookingRegion>())
            LiftIndexToPlayLevel(client, s_clientGridField);
    }

    /// <summary>GridIndex 是不可变 struct（m_y 私有字段、Y 只读属性），装箱改写后整体写回。</summary>
    private static void LiftIndexToPlayLevel(Component synchroniser, FieldInfo field)
    {
        if (synchroniser == null)
            return;
        var boxed = field.GetValue(synchroniser);
        if (boxed == null)
            return;
        var yField = boxed.GetType().GetField("m_y", BindingFlags.Instance | BindingFlags.NonPublic);
        if (yField == null)
            return;
        int y = (int)yField.GetValue(boxed);
        if (y >= 1)
            return;
        yField.SetValue(boxed, 1);
        field.SetValue(synchroniser, boxed);
    }

    /// <summary>2) 锅底 wok_flame 环形火常燃：常驻 EnterCookingRegion（已是 On 时宿主内部无操作）。</summary>
    private static void KeepWokFlameOn()
    {
        foreach (var wok in Object.FindObjectsOfType<ClientWokEffectsCosmeticDecisions>())
        {
            if (wok == null || !IsLargePot(wok.transform))
                continue;
            // 只在锅正处在一个启用的 CookingRegion（火锅灶台）上才点火；
            // 可移动火锅离开灶台后熄火（宿主链路易断，此处直接驱动）。
            if (IsOverBurner(wok.transform))
                wok.EnterCookingRegion();
            else
                wok.ExitCookingRegion();
        }
    }

    /// <summary>锅根是否落在某个启用的 CookingRegion 触发区内（XZ 距离）。
    ///  用锅的碰撞体中心而非 transform 原点判定——锅体模型/碰撞中心在自身局部
    ///  (-0.6, ·, +0.6)，transform 原点即载具中心差半格，用原点会判定失败。</summary>
    private static bool IsOverBurner(Transform t)
    {
        var col = t.GetComponentInChildren<Collider>();
        var center = col != null ? col.bounds.center : t.position;
        foreach (var region in Object.FindObjectsOfType<CookingRegion>())
        {
            if (region == null || !region.enabled || region.m_TriggerArea == null)
                continue;
            var b = region.m_TriggerArea.bounds;
            if (center.x >= b.min.x && center.x <= b.max.x && center.z >= b.min.z && center.z <= b.max.z)
                return true;
        }
        return false;
    }

        /// <summary>3) 大锅汤面：恢复原版 WhenEmpty=0.25，并按当前内容数即时重摆（OnContentChanged 公式）。
    ///  只处理被 Setup 覆写过的（&lt;0），修一次后跳过（重摆交给宿主回调）。</summary>
    private static void FixSoupLevel()
    {
        foreach (var ccd in Object.FindObjectsOfType<ContentsCosmeticDecisions>())
        {
            if (ccd == null || ccd.m_contentsObject == null || !IsLargePot(ccd.transform))
                continue;
            if (ccd.m_contentsYPositionWhenEmpty >= 0f || !s_soupFixed.Add(ccd))
                continue;
            ccd.m_contentsYPositionWhenEmpty = 0.25f;
            RelayoutContents(ccd);
        }
    }

    private static void RelayoutContents(ContentsCosmeticDecisions ccd)
    {
        var container = ccd.m_gameObject != null
            ? ccd.m_gameObject.GetComponent<IngredientContainer>()
            : ccd.GetComponent<IngredientContainer>();
        var clientContainer = ccd.m_gameObject != null
            ? ccd.m_gameObject.GetComponent<ClientIngredientContainer>()
            : ccd.GetComponent<ClientIngredientContainer>();
        if (container == null || clientContainer == null)
            return;
        var contents = clientContainer.GetContents();
        if (contents == null || contents.Length == 0)
            return;
        float fill = Mathf.Clamp01((float)contents.Length / (float)container.m_capacity);
        float y = fill * (ccd.m_contentsYPositionWhenFull - ccd.m_contentsYPositionWhenEmpty)
            + ccd.m_contentsYPositionWhenEmpty;
        var tr = ccd.m_contentsObject.transform;
        tr.localPosition = tr.localPosition.WithY(y);
    }

    /// <summary>4) 煮熟提示音：监听大锅 ServerCookingHandler.IsCooked() 翻转，补发 ImCooked。</summary>
    private static void AlertWhenCooked()
    {
        foreach (var handler in Object.FindObjectsOfType<ServerCookingHandler>())
        {
            if (handler == null || !IsLargePot(handler.transform))
                continue;
            bool cooked;
            if (!s_cookedState.TryGetValue(handler, out cooked))
                cooked = false;
            bool now = handler.IsCooked();
            if (now && !cooked)
                GameUtils.TriggerAudio(GameOneShotAudioTag.ImCooked, handler.gameObject.layer);
            if (now != cooked)
                s_cookedState[handler] = now;
        }
    }

        private static bool IsLargePot(Transform t)
    {
        while (t != null)
        {
            var n = t.name;
            if (n.IndexOf("large_pot", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            // 可移动火锅：wrapper 名（utensil_large_pot_01_pushable / 任意 *pushable* 显示名）
            // 或运行时 child（SO prefabName=pushable_object）都算大锅。
            if (n.IndexOf("pot_01_pushable", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("pushable_object", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            t = t.parent;
        }
        return false;
    }
}
