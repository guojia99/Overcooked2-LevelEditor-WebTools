using System;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

namespace GameModes.Horde
{
	public class ClientHordeLockable : ClientSynchroniserBase
	{
		private HordeLockable m_lockable;

		private ClientInteractable m_interactable;

		private HordeLockableMessage m_message = new HordeLockableMessage();

		public GenericVoid<ClientHordeLockable> m_onLock;

		public GenericVoid<ClientHordeLockable> m_onUnlock;

		public int UnlockCost
		{
			get
			{
				return m_lockable.m_unlockCost;
			}
		}

		public void RegisterOnLock(object handle, GenericVoid<ClientHordeLockable> onLock)
		{
			m_onLock = (GenericVoid<ClientHordeLockable>)Delegate.Combine(m_onLock, onLock);
		}

		public void UnregisterOnLock(object handle, GenericVoid<ClientHordeLockable> onLock)
		{
			m_onLock = (GenericVoid<ClientHordeLockable>)Delegate.Remove(m_onLock, onLock);
		}

		public void RegisterOnUnlock(object handle, GenericVoid<ClientHordeLockable> onUnlock)
		{
			m_onUnlock = (GenericVoid<ClientHordeLockable>)Delegate.Combine(m_onUnlock, onUnlock);
		}

		public void UnregisterOnUnlock(object handle, GenericVoid<ClientHordeLockable> onUnlock)
		{
			m_onUnlock = (GenericVoid<ClientHordeLockable>)Delegate.Remove(m_onUnlock, onUnlock);
		}

		public override EntityType GetEntityType()
		{
			return EntityType.HordeLockable;
		}

		public override void StartSynchronising(Component synchronisedObject)
		{
			base.StartSynchronising(synchronisedObject);
			m_lockable = (HordeLockable)synchronisedObject;
			m_interactable = base.gameObject.RequestComponent<ClientInteractable>();
			if (m_interactable != null)
			{
				m_interactable.SetInteractionSuppressed(false);
			}
		}

		public override void ApplyServerEvent(Serialisable serialisable)
		{
			HordeLockableMessage hordeLockableMessage = (HordeLockableMessage)serialisable;
			if (!hordeLockableMessage.m_locked)
			{
				m_onUnlock(this);
				m_lockable.m_collider.enabled = false;
				if (m_interactable != null)
				{
					m_interactable.SetInteractionSuppressed(true);
				}
			}
			else
			{
				m_onLock(this);
				m_lockable.m_collider.enabled = true;
				if (m_interactable != null)
				{
					m_interactable.SetInteractionSuppressed(false);
				}
			}
		}
	}
}
