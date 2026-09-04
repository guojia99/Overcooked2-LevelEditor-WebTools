using UnityEngine;

namespace CustomStub
{
    /// <summary>
    /// Harmony 补丁集（CustomStub 版，接替原 ServerRespawnCollider / RespawnColliderMessage
    /// 源码覆盖补丁——stub 程序集无法替换宿主类，只能用 Harmony prefix 拦截）。
    ///
    /// 目标方法在 EntryPoint.Install 里手工绑定（HarmonyPatch 特性无法引用
    /// 宿主类型）。前缀方法签名按参数名注入（_gameObject 与宿主方法形参一致）。
    /// </summary>
    internal static class HarmonyPatches
    {
        /// <summary>
        /// ServerRespawnCollider.ObjectAdded(GameObject) 前缀：
        ///  - 玩家触 KillPlane → 先强制脱离可移动火锅（否则玩家成为即将隐藏载具的
        ///    子物体，重生协程随父失效 = 「双人双双落水卡死」）；
        ///  - 可移动火锅自身 → 跳过宿主重生（宿主 DestroyEntity 会把实体连同模型
        ///    永久销毁），坠落/重生由 CustomStub.PushableVoidFall 负责。
        /// 返回 false = 跳过原方法。
        /// </summary>
        private static bool RespawnColliderObjectAddedPrefix(GameObject _gameObject)
        {
            if (_gameObject == null)
                return true;
            try
            {
                if (GameApi.GetComponent(_gameObject, GameApi.PlayerControlsType) != null)
                    PushableVoidFall.DetachPlayerFromVoidFallPots(_gameObject);
                if (PushableVoidFall.ShouldIgnoreKillPlane(_gameObject))
                    return false;
            }
            catch (System.Exception ex)
            {
                StubLog.LogWarn("[CustomStub.Harmony] KillPlane 前缀异常（放行原方法）: " + ex.Message);
            }
            return true;
        }

        /// <summary>供 EntryPoint 手工绑定用的前缀MethodInfo（本程序集内部）。</summary>
        internal static System.Reflection.MethodInfo RespawnColliderObjectAddedPrefixMethod
        {
            get
            {
                return typeof(HarmonyPatches).GetMethod("RespawnColliderObjectAddedPrefix",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            }
        }
    }
}
