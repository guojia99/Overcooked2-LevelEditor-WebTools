using System.Collections.Generic;
using LevelEditorStub;
using UnityEditor;
using UnityEngine;

/// <summary>
/// HUD 订单上限烘焙：把 LevelInfoSO.maxOrderCount 写进场景 CampaignGameEnvironment
/// 实例上的 RecipeFlowGUI（RecipUI）与 KitchenFlowControllerBase（FlowManager），
/// 经 SerializedObject 形成 prefab instance 属性覆盖，随 SaveScene / 导出 bundle
/// 持久化。
///
/// 背景：common01 的 CampaignGameEnvironment.prefab 固定 m_maxOrdersAllowed=5、
/// m_distanceBetweenOrders=5。真机跑原版代码时出单上限经
/// ServerKitchenFlowControllerBase → 订单控制器构造参数取
/// KitchenFlowControllerBase.m_maxOrdersAllowed；HUD 的 RecipeFlowGUI.Awake 按
/// m_maxOrdersAllowed 分配桌号池 m_occupiedTables，并发单数超过它时
/// ClaimUnoccupiedTable 会给出重复桌号或 -1，ReleaseTable(-1) 数组越界崩溃。
/// 烘焙后场景自带正确值，不依赖外部模组。
///
/// 间距：m_distanceBetweenOrders 为相邻订单票像素间距，n&gt;5 时压到 0
/// （m_distanceFromEndOfScreen 本就 5px 贴边，不动）。n 上限 10
/// （与 ClampOrderCount / 前端 max=10 对齐）。
///
/// ⚠ 本类是 m_distanceBetweenOrders 的【唯一】写入方（2026-09-14）：宿主
/// PseudoPrefabManager.SetAssetRef 曾有一份同公式的运行时注入兜底，因违反
/// 「Assets/Scripts/ 只读」硬边界已随该文件还原上游而移除。后果是未经本类
/// 烘焙过的旧场景（磁盘 .unity 里没有该属性覆盖）在编辑器 Play 下会退回
/// prefab 默认 5px——纯外观，写回一次即自愈；m_maxOrdersAllowed 的运行时
/// 注入属上游原有代码，不受影响，不存在 ReleaseTable(-1) 崩溃风险。
/// </summary>
public static class LayoutEditorHudOrderLimits
{
    /// <summary>对当前激活场景烘焙订单上限与间距覆盖；返回警告文本（null = 正常）。</summary>
    public static string BakeActiveScene()
    {
        var stub = Object.FindObjectOfType<PseudoPrefabManagerStub>();
        if (stub == null || stub.levelInfo == null)
            return "[HudOrderLimits] 场景缺少 PseudoPrefabManagerStub/levelInfo，跳过订单上限烘焙";

        var n = Mathf.Clamp(stub.levelInfo.maxOrderCount, 1, 10);
        var distance = n > 5 ? 0f : 5f;
        var warnings = new List<string>();

        var gui = stub.RecipeUIGO != null ? stub.RecipeUIGO.GetComponent<RecipeFlowGUI>() : null;
        if (gui == null)
        {
            warnings.Add("RecipeUIGO 未绑定或缺少 RecipeFlowGUI");
        }
        else
        {
            var so = new SerializedObject(gui);
            var maxProp = so.FindProperty("m_maxOrdersAllowed");
            var distProp = so.FindProperty("m_distanceBetweenOrders");
            if (maxProp == null || distProp == null)
                warnings.Add("RecipeFlowGUI 序列化字段缺失（脚本版本异常）");
            else
            {
                maxProp.intValue = n;
                distProp.floatValue = distance;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        var flow = stub.FlowManagerGO != null ? stub.FlowManagerGO.GetComponent<KitchenFlowControllerBase>() : null;
        if (flow == null)
        {
            warnings.Add("FlowManagerGO 未绑定或缺少 KitchenFlowControllerBase");
        }
        else
        {
            var so = new SerializedObject(flow);
            var maxProp = so.FindProperty("m_maxOrdersAllowed");
            if (maxProp == null)
                warnings.Add("KitchenFlowControllerBase 序列化字段缺失（脚本版本异常）");
            else
            {
                maxProp.intValue = n;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        if (warnings.Count == 0)
            return null;
        return "[HudOrderLimits] n=" + n + "：" + string.Join("; ", warnings.ToArray());
    }
}
