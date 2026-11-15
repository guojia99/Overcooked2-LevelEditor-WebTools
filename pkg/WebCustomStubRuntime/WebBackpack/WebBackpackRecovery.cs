using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

namespace CustomStub
{
    public partial class WebBackpackStorage
    {
        private sealed class PendingTransfer
        {
            internal bool Store;
            internal object Carrier;
            internal GameObject Item;
            internal StoredSlot Slot;
            internal bool DestructionConfirmed;
        }

        private PendingTransfer _pendingTransfer;
        private static readonly HashSet<GameObject> s_reservedItems = new HashSet<GameObject>();
        private float _nextRecovery;

        private bool InventoryLocked { get { return _pendingTransfer != null || _dispensing; } }
        internal static bool HasRecoveryReservations { get { return s_reservedItems.Count > 0; } }
        internal static bool IsRecoveryReserved(GameObject item) { return item != null && s_reservedItems.Contains(item); }
        internal static bool InspectForRecovery(object carrier, out GameObject item) { return TryInspectCarried(carrier, out item); }

        private static bool TryInspectCarried(object carrier, out GameObject item)
        {
            item = null;
            if (carrier == null)
                return false;
            var method = carrier.GetType().GetMethod("InspectCarriedItem", BindingFlags.Public | BindingFlags.Instance,
                null, Type.EmptyTypes, null);
            if (method == null)
                return false;
            try { item = method.Invoke(carrier, null) as GameObject; return true; }
            catch (Exception) { return false; } // Inspection failure is never evidence of an empty hand.
        }

        private void RetryPendingTransfer()
        {
            var pending = _pendingTransfer;
            if (pending == null || Time.realtimeSinceStartup < _nextRecovery)
                return;
            _nextRecovery = Time.realtimeSinceStartup + 0.5f;
            try
            {
                GameObject held;
                if (!TryInspectCarried(pending.Carrier, out held))
                    return;
                if (HasDifferentCarrier(pending.Item, pending.Carrier))
                {
                    WebBackpackPrefabDirectory.Diagnostic("recovery-new-owner", "reserved item has a different carrier; detach/destruction refused");
                    return;
                }
                if (held == pending.Item && pending.Item != null)
                {
                    // DestroyCarriedItem can hide/broadcast destruction, then throw before clearing
                    // its hand slot. TakeItem is idempotently retried against that exact item only.
                    var take = pending.Carrier.GetType().GetMethod("TakeItem", BindingFlags.Public | BindingFlags.Instance,
                        null, Type.EmptyTypes, null);
                    if (take != null)
                    {
                        try
                        {
                            WebBackpackRecoveryIsolation.RecoveryCall = true;
                            WebBackpackRecoveryIsolation.RecoveryCarrier = pending.Carrier;
                            take.Invoke(pending.Carrier, null);
                        }
                        catch (Exception ex) { WebBackpackPrefabDirectory.Diagnostic("recovery-take", "pending hand clear: " + ex.Message); }
                        finally { WebBackpackRecoveryIsolation.RecoveryCall = false; WebBackpackRecoveryIsolation.RecoveryCarrier = null; }
                    }
                    if (!TryInspectCarried(pending.Carrier, out held))
                        return;
                    if (held == pending.Item)
                    {
                        if (!pending.Store && pending.Item != null)
                            pending.Item.SetActive(true);
                        WebBackpackPrefabDirectory.Diagnostic("recovery-held", "transfer locked; exact hand item still held, retrying");
                        return;
                    }
                }
                // A different carried object is never touched. Store commits require an explicit
                // empty-hand confirmation; rollback destruction only requires our item detached.
                if (pending.Store && held != null)
                    return;
                if (HasDifferentCarrier(pending.Item, pending.Carrier))
                {
                    WebBackpackPrefabDirectory.Diagnostic("recovery-new-owner", "reserved item acquired another carrier; destruction refused");
                    return;
                }
                if (pending.Item != null && IsPhysicallyAttached(pending.Item))
                {
                    WebBackpackPrefabDirectory.Diagnostic("recovery-owner", "item still attached to an owner/station; destruction refused");
                    return;
                }
                if (pending.Item != null && (!pending.DestructionConfirmed || pending.Item.activeSelf))
                {
                    var destroy = GameApi.NetworkUtilsType != null ? GameApi.NetworkUtilsType.GetMethod("DestroyObjectsRecursive",
                        BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(GameObject) }, null) : null;
                    if (destroy == null)
                        return;
                    try
                    {
                        destroy.Invoke(null, new object[] { pending.Item });
                        pending.DestructionConfirmed = true;
                    }
                    catch (Exception ex) { WebBackpackPrefabDirectory.Diagnostic("recovery-destroy", "pending destruction: " + ex.Message); }
                    if (!pending.DestructionConfirmed || (pending.Item != null && pending.Item.activeSelf))
                        return;
                }
                if (pending.Store)
                {
                    // Re-read after destruction callbacks; they may have changed the hand slot.
                    if (!TryInspectCarried(pending.Carrier, out held) || held != null)
                        return;
                    WebBackpackPrefabDirectory.Diagnostic("store:" + pending.Slot.Identity, "store confirmed " + pending.Slot.Describe());
                }
                _pendingTransfer = null;
                s_reservedItems.Remove(pending.Item);
                WebBackpackPrefabDirectory.Diagnostic("recovery-complete", "pending transfer resolved; inventory unlocked");
            }
            catch (Exception ex)
            {
                WebBackpackPrefabDirectory.Diagnostic("recovery-error", "transfer remains locked: " + ex.Message);
            }
        }

        private void BeginStoreTransfer(object carrier, GameObject item, StoredSlot slot)
        {
            _pendingTransfer = new PendingTransfer { Store = true, Carrier = carrier, Item = item, Slot = slot };
            s_reservedItems.Add(item);
            _nextRecovery = 0f;
            try
            {
                WebBackpackRecoveryIsolation.RecoveryCall = true;
                WebBackpackRecoveryIsolation.RecoveryCarrier = carrier;
                GameApi.CarrierDestroyCarriedMethod.Invoke(carrier, null);
                _pendingTransfer.DestructionConfirmed = true;
            }
            catch (Exception ex) { WebBackpackPrefabDirectory.Diagnostic("store-destroy", "destroy carried incomplete: " + ex.Message); }
            finally { WebBackpackRecoveryIsolation.RecoveryCall = false; WebBackpackRecoveryIsolation.RecoveryCarrier = null; }
            GameObject held;
            if (_pendingTransfer.DestructionConfirmed && TryInspectCarried(carrier, out held) && held == null && (item == null || !item.activeSelf))
            {
                _pendingTransfer = null;
                s_reservedItems.Remove(item);
                WebBackpackPrefabDirectory.Diagnostic("store:" + slot.Identity, "store confirmed " + slot.Describe());
                return;
            }
            RetryPendingTransfer();
        }

        private void BeginRollbackTransfer(object carrier, GameObject spawned)
        {
            _pendingTransfer = new PendingTransfer { Carrier = carrier, Item = spawned };
            s_reservedItems.Add(spawned);
            _nextRecovery = 0f;
            RetryPendingTransfer();
        }

        private static bool HasDifferentCarrier(GameObject item, object original)
        {
            if (item == null) return false;
            var entities = GameApi.GetEntitiesByGameObject();
            if (entities == null) return true; // Cannot prove ownership: retain the lock.
            var type = GameApi.Find("ServerPlayerAttachmentCarrier");
            if (type == null) return true;
            foreach (System.Collections.DictionaryEntry entity in entities)
            {
                var go = entity.Key as GameObject;
                var carrier = go != null ? go.GetComponent(type) : null;
                if (carrier == null || ReferenceEquals(carrier, original)) continue;
                GameObject held;
                if (!TryInspectCarried(carrier, out held)) return true;
                if (held == item) return true;
            }
            return false;
        }
    }
}
