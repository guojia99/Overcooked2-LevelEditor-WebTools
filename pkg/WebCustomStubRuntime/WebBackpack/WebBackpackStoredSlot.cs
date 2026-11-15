using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace CustomStub
{
    public partial class WebBackpackStorage
    {
        private sealed class StoredSlot
        {
            internal object Node;
            internal WebBackpackPrefabDirectory.Entry Entry;
            internal bool HasWorkable;
            internal int Progress;
            internal int SubProgress;
            internal bool HasCookable;

            internal string Identity { get { return Entry != null ? Entry.Key : "node-only"; } }

            internal string Describe()
            {
                return Identity + " prefab=" + (Entry != null ? Entry.Prefab.name : "none")
                    + " spawn=" + (Entry != null ? Entry.Page + "/" + Entry.LocalId : "none")
                    + " stage=" + (Entry != null ? Entry.Stage : -1)
                    + " progress=" + Progress + " sub=" + SubProgress
                    + " cookable=" + HasCookable + " cooking=0"
                    + " node=" + (Node != null ? Node.GetType().Name : "null");
            }
        }

        private readonly List<StoredSlot> _slots = new List<StoredSlot>();
        private WebBackpackPrefabDirectory _directory;
        private bool _directoryBuilding;
        private bool _directoryReady;
        private bool _dispensing;

        internal void PreparePrefabDirectoryBeforeScan()
        {
            if (_directory != null || _directoryBuilding || _backpackGo == null)
                return;
            // Only FlushPendingAssemblies at the synchronous ScanEntities entry may call this.
            // Never create pages from Start/Update or a StartSynchronising fallback.
            _directoryBuilding = true;
            try
            {
                _directory = new WebBackpackPrefabDirectory();
                _directoryReady = _directory.BuildAndRegister(_backpackGo);
                WebBackpackDirectoryHandshake.Register(this);
            }
            finally
            {
                _directoryBuilding = false;
            }
        }

        internal bool TryGetDirectoryHandshakeRow(out string row)
        {
            row = _directory != null && _backpackGo != null ? _directory.HandshakeRow(_backpackGo) : "unprepared";
            return _directoryReady && _directory.Verify(_backpackGo) && _directory.PagesLinked();
        }

        private bool VerifyLocalDirectory()
        {
            if (_directoryReady && _directory != null && _backpackGo != null
                && _directory.Verify(_backpackGo)) return true;
            _directoryReady = false;
            WebBackpackDirectoryHandshake.Invalidate();
            return false;
        }

        internal bool CanDecodeSpawnPage(int localId, GameObject page)
        {
            if (!VerifyLocalDirectory() || !_directory.ContainsPageSlot(page, localId)) return false;
            return WebBackpackDirectoryHandshake.CanDecodeConfirmedDirectory();
        }

        internal void CheckPrefabDirectory()
        {
            if (!VerifyLocalDirectory())
            {
                _directoryReady = false;
                WebBackpackPrefabDirectory.Diagnostic("not-prepared", "directory not prepared before scan; physical items retained");
            }
        }

        internal bool AllowPrefabRegistration(GameObject spawner, GameObject prefab)
        {
            if (_directoryBuilding)
                return true;
            var entry = _directoryReady ? _directory.Find(prefab) : null;
            if (entry != null && entry.Spawner == spawner)
                return true; // Native client callbacks attach to the already assigned ID.
            WebBackpackPrefabDirectory.Diagnostic("late-register",
                "late registration blocked: " + (prefab != null ? prefab.name : "null"));
            return false;
        }

        internal static bool IsSimpleIngredientPrefab(GameObject prefab)
        {
            return IsBareHandNodeType(NodeFromPrefab(prefab));
        }

        internal static object SimpleNodeFromPrefab(GameObject prefab)
        {
            if (HasCookableIngredient(prefab)) return null; // Raw wrapper is not a simple-node identity.
            var node = NodeFromPrefab(prefab);
            if (node == null)
                node = NodeFromWorkableSource(prefab);
            return IsBareHandNodeType(node) ? node : null;
        }

        private bool AddInitialSlot(object container, GameObject prefab, object node)
        {
            // Initial fill happens only after pre-scan registration, including node-only fills.
            // Never create a late directory after the native entity enumeration has started.
            if (!VerifyLocalDirectory()) return false;
            var entry = _directoryReady && prefab != null ? _directory.Find(prefab) : null;
            if (prefab == null && _directoryReady && IsBareHandNodeType(node))
                entry = _directory.FindUniqueSimpleNode(node);
            if (prefab != null && (entry == null || !entry.Storable))
            {
                WebBackpackPrefabDirectory.Diagnostic("initial:" + prefab.name,
                    "initial physical source unavailable: " + prefab.name);
                return false;
            }
            int count = InvokeInt(container, GameApi.IngredientGetContentsCountMethod);
            if (!SlotsMatch(container, count))
                return false;
            var slot = new StoredSlot { Node = node, Entry = entry };
            var physicalPrefab = entry != null ? entry.Prefab : prefab;
            if (physicalPrefab != null && !IsUncookedPhysical(physicalPrefab, true)) return false;
            slot.HasCookable = HasCookableIngredient(physicalPrefab);
            if (slot.HasCookable)
            {
                // The initial-fill resolver constructs the native Raw wrapper from prefab data.
                // Validate it instead of silently replacing a different cooked/flattened node.
                if (!IsExactRawCookableNode(node, physicalPrefab)) return false;
                slot.Node = node;
                if (slot.Node == null) return false;
            }
            slot.HasWorkable = physicalPrefab != null && GameApi.WorkableItemType != null
                && physicalPrefab.GetComponent(GameApi.WorkableItemType) != null;
            if (!AppendSlot(container, slot))
                return false;
            WebBackpackPrefabDirectory.Diagnostic("initial:" + slot.Identity, "initial " + slot.Describe());
            return true;
        }

        private bool TryCaptureSlot(GameObject item, object node, out StoredSlot slot)
        {
            slot = null;
            if (InventoryLocked || s_reservedItems.Contains(item) || node == null || !VerifyLocalDirectory() || !WebBackpackDirectoryHandshake.Ready()
                || ExactContents(GetServerIngredient()) == null)
                return false;
            var entry = _directory.FindItem(item);
            if (entry == null || !entry.Storable)
                return false;
            if (!IsUncookedPhysical(item, false)) return false;
            slot = new StoredSlot { Node = node, Entry = entry };
            slot.HasCookable = HasCookableIngredient(item);
            if (slot.HasCookable)
            {
                // Generic IOrderDefinition enumeration may find IngredientProperties first.
                // Prefer the authoritative server cookable node so its Raw wrapper is not lost.
                slot.Node = RuntimeCookableNode(item);
                if (!IsExactRawCookableNode(slot.Node, item)) return false;
            }
            var workable = GameApi.WorkableItemType != null ? item.GetComponent(GameApi.WorkableItemType) : null;
            slot.HasWorkable = workable != null;
            if (slot.HasWorkable)
            {
                var serverType = GameApi.Find("ServerWorkableItem");
                var server = serverType != null ? item.GetComponent(serverType) : null;
                if (server == null)
                    return false;
                var progress = serverType.GetField("m_progress", BindingFlags.NonPublic | BindingFlags.Instance);
                var sub = serverType.GetField("m_subProgress", BindingFlags.NonPublic | BindingFlags.Instance);
                var synchronise = serverType.GetMethod("SynchroniseClientState", BindingFlags.NonPublic | BindingFlags.Instance);
                if (progress == null || sub == null || synchronise == null)
                    return false;
                slot.Progress = (int)progress.GetValue(server);
                slot.SubProgress = (int)sub.GetValue(server);
                // Native WorkableMessage allocates four bits to each progress field.
                if (slot.Progress < 0 || slot.Progress > 15 || slot.SubProgress < 0 || slot.SubProgress > 15)
                    return false;
            }
            return true;
        }

        private bool AppendSlot(object container, StoredSlot slot)
        {
            int count = InvokeInt(container, GameApi.IngredientGetContentsCountMethod);
            if (!SlotsMatch(container, count))
                return false;
            try { GameApi.IngredientAddIngredientMethod.Invoke(container, new[] { slot.Node }); }
            catch (Exception ex) { WebBackpackPrefabDirectory.Diagnostic("add-callback", "add callback failed: " + ex.Message); }
            if (InvokeInt(container, GameApi.IngredientGetContentsCountMethod) != count + 1)
                return false;
            _slots.Add(slot);
            return true;
        }

        private static bool RemoveExactSlotNode(object container, int index)
        {
            // Vanilla RemoveIngredient(index) calls List.Remove(node), using semantic Equals.
            // A rollback of the last raw ingredient could therefore remove an earlier slot.
            var contents = ExactContents(container);
            if (contents == null || index < 0 || index >= contents.Count)
                return false;
            contents.RemoveAt(index);
            InformOfInternalChange(container);
            return true;
        }

        private static System.Collections.IList ExactContents(object container)
        {
            var type = GameApi.ServerIngredientContainerType;
            var field = type != null ? type.GetField("m_contents", BindingFlags.NonPublic | BindingFlags.Instance) : null;
            return container != null && field != null ? field.GetValue(container) as System.Collections.IList : null;
        }

        private bool SlotsMatch(object container, int count)
        {
            if (_slots.Count != count)
            {
                WebBackpackPrefabDirectory.Diagnostic("slot-count", "slot/container count mismatch; inventory retained");
                return false;
            }
            for (int i = 0; i < count; i++)
                if (!ReferenceEquals(_slots[i].Node, InvokeObj(container, GameApi.IngredientGetContentsElementMethod, i)))
                {
                    WebBackpackPrefabDirectory.Diagnostic("slot-order", "slot/container order mismatch; inventory retained");
                    return false;
                }
            return true;
        }

        private static bool RestoreSlot(StoredSlot slot, GameObject spawned)
        {
            if (slot.HasCookable && (!HasCookableIngredient(spawned) || !IsUncookedPhysical(spawned, false)
                || !IsExactRawCookableNode(slot.Node, spawned))) return false;
            if (!slot.HasWorkable)
                return true;
            var type = GameApi.Find("ServerWorkableItem");
            var server = type != null ? spawned.GetComponent(type) : null;
            if (server == null)
                return false;
            var progress = type.GetField("m_progress", BindingFlags.NonPublic | BindingFlags.Instance);
            var sub = type.GetField("m_subProgress", BindingFlags.NonPublic | BindingFlags.Instance);
            var sync = type.GetMethod("SynchroniseClientState", BindingFlags.NonPublic | BindingFlags.Instance);
            if (progress == null || sub == null || sync == null)
                return false;
            progress.SetValue(server, slot.Progress);
            sub.SetValue(server, slot.SubProgress);
            // ServerSpawnPrefab has registered synchronizers and queued SpawnEntity before returning.
            // The native reliable WorkableMessage follows it, restoring visuals on all clients.
            sync.Invoke(server, null);
            return (int)progress.GetValue(server) == slot.Progress && (int)sub.GetValue(server) == slot.SubProgress;
        }

        private bool TryDispenseStoredSlot(object container, object head, int count, object carrier)
        {
            GameObject heldAtStart;
            if (InventoryLocked || !VerifyLocalDirectory() || !WebBackpackDirectoryHandshake.Ready() || !TryInspectCarried(carrier, out heldAtStart) || heldAtStart != null
                || !SlotsMatch(container, count))
                return false;
            var slot = _slots[0];
            if (slot.Entry == null || !ReferenceEquals(head, slot.Node))
                return false; // Node-only recipe initial fill still uses the existing empty-plate path.
            if (GameApi.GetEntityId(slot.Entry.Spawner) == 0)
            {
                WebBackpackPrefabDirectory.Diagnostic("page-unlinked", "spawn page missing network entity; inventory retained");
                return false;
            }
            var carry = carrier.GetType().GetMethod("CarryItem", BindingFlags.Public | BindingFlags.Instance,
                null, new[] { typeof(GameObject) }, null);
            var inspect = carrier.GetType().GetMethod("InspectCarriedItem", BindingFlags.Public | BindingFlags.Instance,
                null, Type.EmptyTypes, null);
            var destroy = GameApi.NetworkUtilsType != null ? GameApi.NetworkUtilsType.GetMethod("DestroyObjectsRecursive",
                BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(GameObject) }, null) : null;
            var take = carrier.GetType().GetMethod("TakeItem", BindingFlags.Public | BindingFlags.Instance,
                null, Type.EmptyTypes, null);
            if (carry == null || inspect == null || destroy == null || take == null)
                return false;
            GameObject spawned = null;
            bool committed = false;
            bool restored = false;
            _dispensing = true;
            try
            {
                WebBackpackPrefabDirectory.BeginSpawnCapture(slot.Entry.Spawner);
                GameObject returned;
                GameObject captured;
                try { returned = GameApi.ServerSpawnPrefab(slot.Entry.Spawner, slot.Entry.Prefab); }
                finally
                {
                    // GameApi returns null when native registration/callback throws after an
                    // instance was already created. Retain that instance for rollback cleanup.
                    captured = WebBackpackPrefabDirectory.EndSpawnCapture();
                    spawned = captured;
                }
                if (returned == null)
                    return false;
                spawned = returned;
                if (spawned == null || !RestoreSlot(slot, spawned))
                {
                    WebBackpackPrefabDirectory.Diagnostic("restore-failed", "spawn/state restoration failed; inventory retained " + slot.Describe());
                    return false;
                }
                restored = true;
                carry.Invoke(carrier, new object[] { spawned });
                if (inspect.Invoke(carrier, null) as GameObject != spawned)
                {
                    WebBackpackPrefabDirectory.Diagnostic("carry-failed", "carry confirmation failed; inventory retained " + slot.Describe());
                    return false;
                }
                // Recheck FIFO after native carry callbacks, not just before calling CarryItem.
                if (!TryCommitAttachedSlot(container, head, count, carrier, spawned)) return false;
                committed = true;
                _backpackGo.SendMessage("OnPickupItem", SendMessageOptions.DontRequireReceiver);
                WebBackpackPrefabDirectory.Diagnostic("dispense:" + slot.Identity, "dispense confirmed " + slot.Describe()
                    + " spawnerEntity=" + GameApi.GetEntityId(slot.Entry.Spawner) + " spawnedEntity=" + GameApi.GetEntityId(spawned));
                return true;
            }
            catch (Exception ex)
            {
                // Native CarryItem assigns the hand, attaches, then invokes notifications. A
                // notification failure is not a reason to manufacture a rollback-held duplicate.
                bool attachedCommit = false;
                try { attachedCommit = restored && TryCommitAttachedSlot(container, head, count, carrier, spawned); }
                catch (Exception commitError) { WebBackpackPrefabDirectory.Diagnostic("carry-commit-error", "attached commit unavailable: " + commitError.Message); }
                if (attachedCommit)
                {
                    committed = true;
                    RefreshConfirmedCarry(carrier, spawned);
                    WebBackpackPrefabDirectory.Diagnostic("carry-partial-commit",
                        "carry callback failed after attachment; FIFO committed: " + ex.Message);
                    return true;
                }
                WebBackpackPrefabDirectory.Diagnostic("dispense-error", "dispense failed: " + ex.Message);
                return false;
            }
            finally
            {
                _dispensing = false;
                if (!committed && spawned != null)
                {
                    BeginRollbackTransfer(carrier, spawned);
                }
            }
        }

        private bool TryCommitAttachedSlot(object container, object head, int count, object carrier, GameObject spawned)
        {
            if (!VerifyLocalDirectory()) return false;
            GameObject held;
            if (spawned == null || !TryInspectCarried(carrier, out held) || held != spawned
                || !IsPhysicallyAttached(spawned)) return false;
            int current = InvokeInt(container, GameApi.IngredientGetContentsCountMethod);
            if (current == count && _slots.Count == count && ReferenceEquals(_slots[0].Node, head)
                && SlotsMatch(container, count))
            {
                if (!RemoveExactSlotNode(container, 0)) return false;
                _slots.RemoveAt(0);
                return true;
            }
            // Debit may already have completed before a subsequent animation threw.
            if (current == count - 1 && _slots.Count == count - 1 && SlotsMatch(container, current))
                return true;
            return false;
        }

        private static bool IsPhysicallyAttached(GameObject item)
        {
            var type = GameApi.Find("ServerPhysicalAttachment");
            var attachment = type != null ? item.GetComponent(type) : null;
            var method = type != null ? type.GetMethod("IsAttached", Type.EmptyTypes) : null;
            return attachment != null && method != null && (bool)method.Invoke(attachment, null);
        }

        private static void RefreshConfirmedCarry(object carrier, GameObject item)
        {
            try
            {
                // CarryItem notification may throw before its final ChefCarryMessage. Repair
                // that native message without replaying attachment/notification callbacks.
                var type = GameApi.Find("ServerPlayerAttachmentCarrier");
                if (type == null || !type.IsInstanceOfType(carrier)) return;
                var data = type.GetField("m_ServerData", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(carrier);
                data.GetType().GetField("m_carriableItem").SetValue(data, GameApi.GetEntityId(item));
                var target = data.GetType().GetField("m_playerAttachTarget");
                if (target != null) target.SetValue(data, Enum.ToObject(target.FieldType, 0));
                var send = type.GetMethod("SendServerEvent", BindingFlags.Public | BindingFlags.Instance);
                send.Invoke(carrier, new[] { data });
            }
            catch (Exception ex)
            {
                WebBackpackPrefabDirectory.Diagnostic("carry-refresh", "attached FIFO committed; carry message repair failed: " + ex.Message);
            }
        }
    }
}
