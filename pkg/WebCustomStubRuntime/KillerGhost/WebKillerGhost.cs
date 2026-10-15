using System.Reflection;
using UnityEngine;

namespace CustomStub
{
    /// <summary>
    /// Web 致命鬼魂（commonW4）：包装根上挂原版 RespawnCollider（Hit）+ 触发碰撞体；
    /// 位移由布局编辑器动画组负责，视觉由 DLC07_NPCs_Ghost 伪预制体生成。
    /// 击杀走 ServerRespawnCollider → KillOrRespawn（与火球同款，非 CustomStub 自研）。
    /// </summary>
    public class WebKillerGhost : MonoBehaviour
    {
        public const string TagPrefix = "WebKillerGhost|";

        public const float DefaultHitRadius = 0.45f;

        /// <summary>触发球半径（米）。</summary>
        public float m_hitRadius = DefaultHitRadius;

        /// <summary>false = 保留碰撞体但不致死（调试用）。</summary>
        public bool m_enabled = true;

        /// <summary>npc_ghost | npc_keep | ghost_chef（视觉由 PseudoPrefabNPCStub.pseudoPrefabSO 决定）。</summary>
        public string m_style = "npc_ghost";

        private void Awake()
        {
            ApplyConfig();
        }

        public void ApplyConfig()
        {
            PrepareHitCollider(gameObject, m_hitRadius, m_enabled);
        }

        internal static void PrepareHitCollider(GameObject go, float radius, bool enabled)
        {
            if (go == null)
                return;
            radius = Mathf.Clamp(radius, 0.15f, 2f);

            var sphere = go.GetComponent<SphereCollider>();
            if (sphere == null)
                sphere = go.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = radius;
            sphere.center = new Vector3(0f, 0.6f, 0f);
            sphere.enabled = enabled;

            var rb = go.GetComponent<Rigidbody>();
            if (rb == null)
                rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var rcType = GameApi.RespawnColliderType;
            if (rcType == null)
                return;
            var rc = go.GetComponent(rcType);
            if (rc == null)
                rc = go.AddComponent(rcType);

            SetEnumField(rc, "m_respawnType", 0); // Hit
            SetLayerMaskPlayers(rc, "m_respawnFilter");
            SetBoolField(rc, "m_onlyRespawnables", false);
            var beh = rc as Behaviour;
            if (beh != null)
                beh.enabled = enabled;
        }

        private static void SetEnumField(Component c, string field, int enumIndex)
        {
            var f = c.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f == null)
                return;
            if (f.FieldType.IsEnum)
                f.SetValue(c, System.Enum.ToObject(f.FieldType, enumIndex));
            else
                f.SetValue(c, enumIndex);
        }

        private static void SetBoolField(Component c, string field, bool value)
        {
            var f = c.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null && f.FieldType == typeof(bool))
                f.SetValue(c, value);
        }

        private static void SetLayerMaskPlayers(Component c, string field)
        {
            var f = c.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f == null || f.FieldType != typeof(LayerMask))
                return;
            var players = LayerMask.GetMask("Players");
            if (players == 0)
                players = 256;
            f.SetValue(c, (LayerMask)players);
        }
    }
}
