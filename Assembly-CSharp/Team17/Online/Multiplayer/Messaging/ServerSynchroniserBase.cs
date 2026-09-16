using UnityEngine;

namespace Team17.Online.Multiplayer.Messaging
{
	public class ServerSynchroniserBase : SynchroniserBase, ServerSynchroniser, Synchroniser
	{
		private uint m_uEntityId;

		private uint m_uComponentId;

		public override void StartSynchronising(Component synchronisedObject)
		{
		}

		public override void UpdateSynchronising()
		{
		}

		public override EntityType GetEntityType()
		{
			return EntityType.Unknown;
		}

		public void Initialise(uint uEntityId, uint uComponentId)
		{
			m_uEntityId = uEntityId;
			m_uComponentId = uComponentId;
		}

		public virtual Serialisable GetServerUpdate()
		{
			return null;
		}

		public virtual bool HasTargetedServerUpdates()
		{
			return false;
		}

		public virtual Serialisable GetServerUpdateForRecipient(IOnlineMultiplayerSessionUserId recipient)
		{
			return null;
		}

		public uint GetEntityId()
		{
			return m_uEntityId;
		}

		public uint GetComponentId()
		{
			return m_uComponentId;
		}

		public virtual void SendServerEvent(Serialisable message)
		{
			if (MultiplayerController.IsSynchronisationActive())
			{
				ServerMessenger.EntityEvent(this, message);
			}
		}

		public virtual void SendServerEventToRecipient(IOnlineMultiplayerSessionUserId recipient, Serialisable message)
		{
			if (MultiplayerController.IsSynchronisationActive())
			{
				ServerMessenger.EntityEvent(recipient, this, message);
			}
		}

		public virtual void OnDestroy()
		{
			StopSynchronising();
			EntitySerialisationRegistry.UnregisterObject(base.gameObject);
		}
	}
}
