using System.Collections;
using UnityEngine;
using LevelEditorStub;

namespace CustomStub
{
    /// <summary>
    /// 初始关闭开关的开局外观（2026-09-28，v3.4.0）。
    ///
    /// 背景：宿主 PseudoPrefabSwitch.Setup 无条件把按钮 bit 设为 activeMaterial
    /// （绿），关闭色的切换者是同步期才挂载的 ClientSwitchCosmeticDecisions
    /// （轮询 Interactable.enabled）——开局（intro 运镜期）有一段绿色闪烁，真机
    /// 侧更无任何保障；互锁对的初始按下侧因此表现为「开局全绿」（2026-09-28
    /// 用户实测报告）。
    ///
    /// 职责（挂在开关伪根上，与 SwitchReenable 同位）：
    ///  - 读同物体 PseudoPrefabSwitchStub.startEnabled（LevelEditorStub 随包分发，
    ///    asmdef 直引，无需反射）；true → 什么都不做（防御，正常不会挂上）。
    ///  - 等 child 就绪（扫子树 SwitchCosmeticDecisions，SwitchReenable 同款
    ///    轮询等待模式，最多 20s——真机 child 由关卡加载器在 sceneLoaded 之后
    ///    实例化）后：
    ///      ① m_buttonBit.sharedMaterial = m_inactiveMaterial（开局即关闭色）；
    ///      ② 预置 TriggerDisableScript.m_script.enabled = false（同步握手前就
    ///        不可按；ClientTriggerDisableScript 之后按 m_startEnabled 再写同值，
    ///        无冲突；后续 relay 的 Disable/Reset 走消息通道照常工作）。
    ///  - Renderer/Behaviour 均为 UnityEngine 类型，取到引用后直改；两个材质
    ///    字段经一次性反射缓存（SwitchCosmeticDecisions 在 Assembly-CSharp，
    ///    asmdef 编译期不可见）。
    ///
    /// 通道：编辑器写回烘焙组件（编辑器 Play 直用，StubIO.ApplySwitch 增删）；
    /// 真机场景烘焙件是 Missing Script 死件，由 EntryPoint.HealSwitchStartVisuals
    /// 扫描 PseudoPrefabSwitchStub 补挂——无 tag 载体，stub 组件本身就是权威数据。
    /// </summary>
    public class SwitchStartVisual : MonoBehaviour
    {
        private static System.Reflection.FieldInfo s_buttonBitField;
        private static System.Reflection.FieldInfo s_inactiveField;
        private static bool s_fieldsResolved;

        private IEnumerator Start()
        {
            var stub = GetComponent<PseudoPrefabSwitchStub>();
            if (stub != null && stub.startEnabled)
                yield break;

            float waited = 0f;
            Component cos = null;
            while (true)
            {
                cos = FindCosmetic();
                if (cos != null)
                    break;
                waited += Time.unscaledDeltaTime;
                if (waited > 20f)
                {
                    StubLog.LogWarn("[SwitchStartVisual] 等待按钮 child 超时（20s），关闭外观未应用: " + name);
                    yield break;
                }
                yield return new WaitForSeconds(0.25f);
            }

            ApplyMaterial(cos);
            DisableInteractable();
            StubLog.Log("[SwitchStartVisual] " + name +
                " 初始关闭：已应用关闭材质并预禁用交互（开局即关闭色）");
        }

        /// <summary>子树找 SwitchCosmeticDecisions（Assembly-CSharp 全局类型，
        /// 按简单名匹配——ButtonLogicRelay 解析 PseudoPrefab 同款模式）。</summary>
        private Component FindCosmetic()
        {
            var found = GetComponentsInChildren<Component>(true);
            for (int i = 0; i < found.Length; i++)
            {
                var c = found[i];
                if (c != null && c.GetType().Name == "SwitchCosmeticDecisions")
                    return c;
            }
            return null;
        }

        private void ApplyMaterial(Component cos)
        {
            ResolveFields();
            var bit = s_buttonBitField != null ? s_buttonBitField.GetValue(cos) as Renderer : null;
            var mat = s_inactiveField != null ? s_inactiveField.GetValue(cos) as Material : null;
            if (bit == null || mat == null)
            {
                StubLog.LogWarn("[SwitchStartVisual] " + name +
                    " 缺少 buttonBit/inactiveMaterial 引用，关闭材质未应用");
                return;
            }
            bit.sharedMaterial = mat;
        }

        /// <summary>预禁用交互：TriggerDisableScript.m_script（通常 Interactable）。
        /// GameApi 已缓存该类型/字段（SwitchReenable 同款）。</summary>
        private void DisableInteractable()
        {
            if (GameApi.TriggerDisableType == null)
                return;
            var found = GetComponentsInChildren(GameApi.TriggerDisableType, true);
            if (found.Length == 0 || GameApi.DisableScriptField == null)
                return;
            var behaviour = found[0] != null
                ? GameApi.DisableScriptField.GetValue(found[0]) as Behaviour
                : null;
            if (behaviour != null)
                behaviour.enabled = false;
        }

        private static void ResolveFields()
        {
            if (s_fieldsResolved) return;
            s_fieldsResolved = true;
            try
            {
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (asm == null) continue;
                    var t = asm.GetType("SwitchCosmeticDecisions", false);
                    if (t == null) continue;
                    s_buttonBitField = t.GetField("m_buttonBit");
                    s_inactiveField = t.GetField("m_inactiveMaterial");
                    break;
                }
            }
            catch (System.Exception ex)
            {
                StubLog.LogWarn("[SwitchStartVisual] 反射解析 SwitchCosmeticDecisions 失败: " + ex.Message);
            }
        }
    }
}
