using UnityEngine;

namespace Team17.Online.Multiplayer.Messaging
{
	public class ClientSynchroniserBase : SynchroniserBase, ClientSynchroniser, Synchroniser
	{
		private int m_iLastSequenceNumberProcessed = -1;

		private float m_lastUpdateTimeStamp;

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

		public virtual bool IsValidServerUpdateSequenceNumber(uint uSequence)
		{
			if (uSequence == uint.MaxValue)
			{
				return true;
			}
			return Mailbox.CheckSequenced(m_iLastSequenceNumberProcessed, (int)uSequence);
		}

		public virtual void SetLastServerUpdateSequenceNumber(uint uSequence)
		{
			m_iLastSequenceNumberProcessed = (int)uSequence;
		}

		public virtual bool IsValidLastUpdateTimeStamp(float timeStamp, float diff)
		{
			return timeStamp - m_lastUpdateTimeStamp > diff;
		}

		public virtual void SetLastUpdateTimeStamp(float timeStamp)
		{
			m_lastUpdateTimeStamp = timeStamp;
		}

		public virtual void ApplyServerUpdate(Serialisable serialisable)
		{
		}

		public virtual void ApplyServerEvent(Serialisable serialisable)
		{
		}

		protected virtual void OnDestroy()
		{
			EntitySerialisationRegistry.UnregisterObject(base.gameObject);
		}
	}
}
