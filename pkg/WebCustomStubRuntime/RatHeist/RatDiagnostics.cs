using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace CustomStub
{
    // Observes native interaction traffic; never sends or consumes messages.
    internal static class RatDiagnostics
    {
        private const BindingFlags Instance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly HashSet<GameObject> s_rats = new HashSet<GameObject>();
        private static readonly Dictionary<string, float> s_next = new Dictionary<string, float>();
        private static bool s_installed;
        private static bool s_warned;
        private static int s_enabled = -1;
        private static readonly Dictionary<GameObject, float> s_selectionNext = new Dictionary<GameObject, float>();
        private static readonly List<MethodBase> s_targets = new List<MethodBase>();

        internal static void Register(GameObject rat)
        {
            if (rat != null) s_rats.Add(rat);
        }

        internal static void OnSceneChanged()
        {
            var harmony = new Harmony("oc2.customstub.ratdiagnostics");
            foreach (var target in s_targets)
            {
                try { harmony.Unpatch(target, HarmonyPatchType.All, "oc2.customstub.ratdiagnostics"); }
                catch (Exception ex) { StubLog.LogWarn("[RatNet] diagnostic unpatch failed: " + ex.Message); }
            }
            s_targets.Clear();
            s_installed = false;
            s_rats.Clear();
            s_next.Clear();
            s_selectionNext.Clear();
            s_warned = false;
        }

        internal static bool Enabled
        {
            get
            {
                if (StubLog.Verbose) return true;
                if (s_enabled < 0)
                {
                    try { s_enabled = Environment.GetEnvironmentVariable("OC2_RAT_DIAGNOSTICS") == "1" ? 1 : 0; }
                    catch { s_enabled = 0; }
                }
                return s_enabled == 1;
            }
        }

        internal static void Install()
        {
            if (s_installed || !Enabled || s_rats.Count == 0) return;
            s_installed = true;
            var harmony = new Harmony("oc2.customstub.ratdiagnostics");
            int installed = 0;
            var messenger = GameApi.Find("ClientMessenger");
            if (messenger != null)
            {
                foreach (var method in messenger.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (method.Name == "ChefEventMessage" && method.GetParameters().Length == 3)
                        installed += Patch(harmony, method, "ClientSendPrefix", false);
                }
            }
            var server = GameApi.Find("ServerPlayerControlsImpl_Default");
            installed += Patch(harmony, server != null ? server.GetMethod("ReceiveTriggerInteractEvent", Instance) : null,
                "ServerReceivePrefix", false);
            installed += Patch(harmony, server != null ? server.GetMethod("OnChefEvent", Instance) : null,
                "ServerMessagePrefix", false);
            var controls = GameApi.Find("PlayerControls");
            installed += Patch(harmony, controls != null ? controls.GetMethod("UpdateNearbyObjects", Instance) : null,
                "SelectionPostfix", true);
            StubLog.Log("[RatNet] diagnostics hooks=" + installed + " (per rat/stage <= 1 per second)");
        }

        private static int Patch(Harmony harmony, MethodInfo target, string handler, bool postfix)
        {
            if (target == null)
            {
                StubLog.LogWarn("[RatNet] diagnostic hook unavailable: " + handler);
                return 0;
            }
            try
            {
                var hook = new HarmonyMethod(typeof(RatDiagnostics).GetMethod(handler, BindingFlags.NonPublic | BindingFlags.Static));
                if (postfix) harmony.Patch(target, null, hook);
                else harmony.Patch(target, hook);
                s_targets.Add(target);
                return 1;
            }
            catch (Exception ex)
            {
                StubLog.LogWarn("[RatNet] diagnostic hook failed: " + target.Name + ": " + ex.Message);
                return 0;
            }
        }

        private static GameObject AsGameObject(object value)
        {
            var go = value as GameObject;
            var component = value as Component;
            return component != null ? component.gameObject : go;
        }

        private static void ClientSendPrefix(object __0, GameObject __1, object __2)
        {
            if (s_rats.Count == 0 || !Enabled) return;
            try
            {
                var target = AsGameObject(__2);
                if (target != null && s_rats.Contains(target) && __0 != null && __0.ToString() == "TriggerInteract")
                    Trace("selected/send-TriggerInteract", target, __1, false);
            }
            catch (Exception ex) { Warn(ex); }
        }

        private static void ServerReceivePrefix(object __instance, GameObject __0)
        {
            if (s_rats.Count == 0 || !Enabled) return;
            try
            {
                if (__0 != null && s_rats.Contains(__0))
                    Trace("host-receive-TriggerInteract", __0, AsGameObject(__instance), false);
            }
            catch (Exception ex) { Warn(ex); }
        }

        private static void SelectionPostfix(object __instance)
        {
            if (s_rats.Count == 0 || !Enabled) return;
            try
            {
                var chef = AsGameObject(__instance);
                if (chef == null) return;
                float next;
                if (s_selectionNext.TryGetValue(chef, out next) && Time.realtimeSinceStartup < next) return;
                s_selectionNext[chef] = Time.realtimeSinceStartup + 0.25f;
                var prop = __instance.GetType().GetProperty("CurrentInteractionObjects", Instance);
                var objects = prop != null ? prop.GetValue(__instance, null) : null;
                if (objects == null) return;
                var field = objects.GetType().GetField("m_interactable", Instance);
                var target = field != null ? AsGameObject(field.GetValue(objects)) : null;
                if (target != null && s_rats.Contains(target))
                    Trace("client-selected", target, chef, false);
                else
                {
                    // A nearby unselected rat is useful evidence when the scan cannot see it.
                    foreach (var rat in s_rats)
                    {
                        if (rat != null && (rat.transform.position - chef.transform.position).sqrMagnitude < 6.25f)
                            Trace("client-not-selected target=" + (target != null ? target.name : "none"), rat, chef, false);
                    }
                }
            }
            catch (Exception ex) { Warn(ex); }
        }

        private static void ServerMessagePrefix(object __instance, object __1)
        {
            if (s_rats.Count == 0 || !Enabled || __1 == null) return;
            try
            {
                var type = __1.GetType();
                var eventType = type.GetField("EventType");
                if (eventType == null || eventType.GetValue(__1).ToString() != "TriggerInteract") return;
                uint chefId = (uint)type.GetField("ChefEntityID").GetValue(__1);
                var chef = AsGameObject(__instance);
                // OnChefEvent is registered once per chef. Only the receiving chef logs it.
                if (chef == null || GameApi.GetEntityId(chef) != chefId) return;
                uint targetId = (uint)type.GetField("EntityID").GetValue(__1);
                var registry = GameApi.EntityRegistryType;
                var getEntry = registry != null ? registry.GetMethod("GetEntry", new[] { typeof(uint) }) : null;
                var entry = getEntry != null ? getEntry.Invoke(null, new object[] { targetId }) : null;
                var field = entry != null ? entry.GetType().GetField("m_GameObject") : null;
                var target = field != null ? field.GetValue(entry) as GameObject : null;
                foreach (var rat in s_rats)
                {
                    if (rat != null && (target == rat || (rat.transform.position - chef.transform.position).sqrMagnitude < 9f))
                        Trace("host-message targetId=" + targetId + " resolved=" + (target != null ? target.name : "none"), rat, chef, false);
                }
            }
            catch (Exception ex) { Warn(ex); }
        }

        private static void Warn(Exception ex)
        {
            if (s_warned) return;
            s_warned = true;
            StubLog.LogWarn("[RatNet] diagnostic read failed: " + ex.Message);
        }

        internal static void Trace(string stage, GameObject rat, GameObject chef, bool force)
        {
            if (rat == null || (!force && !Enabled)) return;
            try
            {
                int detail = stage.IndexOf(' ');
                string key = (detail >= 0 ? stage.Substring(0, detail) : stage) + ":" + rat.GetInstanceID();
                float next;
                float now = Time.realtimeSinceStartup;
                if (s_next.TryGetValue(key, out next) && now < next) return;
                s_next[key] = now + 1f;
                var cli = rat.GetComponent(GameApi.Find("ClientInteractable")) as Behaviour;
                var srv = rat.GetComponent(GameApi.Find("ServerInteractable")) as Behaviour;
                int colliders = 0;
                foreach (var collider in rat.GetComponentsInChildren<Collider>(true))
                    if (collider != null && collider.enabled) colliders++;
                StubLog.Log("[RatNet] " + stage + " rat=" + rat.name + " id=" + GameApi.GetEntityId(rat)
                    + " chef=" + (chef != null ? chef.name : "none") + " chefId=" + GameApi.GetEntityId(chef)
                    + " pos=" + rat.transform.position.ToString("0.00")
                    + " CliInteract=" + State(cli) + " SrvInteract=" + State(srv)
                    + " Interactable=" + Has(rat, "Interactable")
                    + " DynamicGrid=" + Has(rat, "DynamicGridLocation")
                    + " CliWorld=" + Has(rat, "Team17.Online.Multiplayer.Messaging.ClientWorldObjectSynchroniser")
                    + " SrvWorld=" + Has(rat, "Team17.Online.Multiplayer.Messaging.ServerWorldObjectSynchroniser")
                    + " CliCarrier=" + Has(rat, "ClientPlayerAttachmentCarrier")
                    + " SrvCarrier=" + Has(rat, "ServerPlayerAttachmentCarrier")
                    + " colliders=" + colliders + " layer=" + rat.layer
                    + " canInteract=" + CanInteract(cli, chef)
                    + " distance=" + (chef != null ? Vector3.Distance(chef.transform.position, rat.transform.position).ToString("0.00") : "n/a"));
            }
            catch (Exception ex) { Warn(ex); }
        }

        private static string Has(GameObject go, string name)
        {
            var type = GameApi.Find(name);
            if (type == null) return "type-missing";
            var component = go.GetComponent(type);
            var behaviour = component as Behaviour;
            return behaviour != null ? State(behaviour) : (component != null ? "yes" : "missing");
        }

        private static string State(Behaviour behaviour)
        {
            return behaviour == null ? "missing" : (behaviour.enabled ? "enabled" : "disabled");
        }

        private static string CanInteract(Behaviour behaviour, GameObject chef)
        {
            if (behaviour == null || chef == null) return "n/a";
            var method = behaviour.GetType().GetMethod("CanInteract", new[] { typeof(GameObject) });
            return method != null ? method.Invoke(behaviour, new object[] { chef }).ToString() : "missing";
        }
    }
}
