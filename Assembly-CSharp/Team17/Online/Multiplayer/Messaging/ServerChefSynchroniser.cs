using UnityEngine;

namespace Team17.Online.Multiplayer.Messaging
{
	public class ServerChefSynchroniser : ServerWorldObjectSynchroniser
	{
		private Rigidbody m_Rigidbody;

		private ChefPositionMessage m_ChefData = new ChefPositionMessage();

		private ServerInputReceiver m_InputReceiver;

		private IOnlineMultiplayerSessionUserId m_Owner;

		private bool m_bOwnerCalculated;

		private IOnlineMultiplayerSessionCoordinator m_SessionCoordinator;

		public override void StartSynchronising(Component synchronisedObject)
		{
			m_Rigidbody = GetComponent<Rigidbody>();
			m_ChefData.WorldObject = GetMessageData();
			m_InputReceiver = GetComponent<ServerInputReceiver>();
			base.StartSynchronising(synchronisedObject);
		}

		public override void Awake()
		{
			IOnlinePlatformManager onlinePlatformManager = GameUtils.RequireManagerInterface<IOnlinePlatformManager>();
			m_SessionCoordinator = onlinePlatformManager.OnlineMultiplayerSessionCoordinator();
			base.Awake();
		}

		public override EntityType GetEntityType()
		{
			return EntityType.Chef;
		}

		public override Serialisable GetServerUpdate()
		{
			m_bSleepAllowed = false;
			base.GetServerUpdate();
			return null;
		}

		public override void SendServerEvent(Serialisable message)
		{
			if (m_SessionCoordinator == null)
			{
				return;
			}
			IOnlineMultiplayerSessionUserId[] array = m_SessionCoordinator.Members();
			if (array == null)
			{
				return;
			}
			foreach (IOnlineMultiplayerSessionUserId recipient in array)
			{
				Serialisable serialisable = BuildMessageForRecipient(recipient, true);
				if (serialisable != null)
				{
					SendServerEventToRecipient(recipient, serialisable);
				}
			}
		}

		public override bool HasTargetedServerUpdates()
		{
			return true;
		}

		public Serialisable BuildMessageForRecipient(IOnlineMultiplayerSessionUserId recipient, bool bForce = false)
		{
			if (recipient == null)
			{
				return null;
			}
			if (!m_bStartedSynchronising)
			{
				return null;
			}
			if (null == m_Transform)
			{
				return null;
			}
			if (m_bSleepAllowed && !m_bOwnerCalculated)
			{
				return null;
			}
			if (bForce || IsWorldObjectActive() || m_Rigidbody.velocity != m_ChefData.Velocity)
			{
				m_ChefData.Velocity = m_Rigidbody.velocity;
				m_ChefData.NetworkTime = ClientTime.Time();
				m_ChefData.ClientTimeStamp = m_InputReceiver.GetLastClientInputTimeStamp();
				return m_ChefData;
			}
			return null;
		}

		public override Serialisable GetServerUpdateForRecipient(IOnlineMultiplayerSessionUserId recipient)
		{
			return BuildMessageForRecipient(recipient);
		}

		protected override void OnGameStateChanged(GameState state, GameStateMessage.GameStatePayload payload)
		{
			base.OnGameStateChanged(state, payload);
			if (state != GameState.StartEntities)
			{
				return;
			}
			for (int i = 0; i < ServerUserSystem.m_Users.Count; i++)
			{
				User user = ServerUserSystem.m_Users._items[i];
				if (user.EntityID == GetEntityId() || user.Entity2ID == GetEntityId())
				{
					m_Owner = user.SessionId;
					break;
				}
			}
			m_bOwnerCalculated = true;
		}

		protected override bool SendResumeData(IOnlineMultiplayerSessionUserId sessionUserId)
		{
			RefreshParent();
			m_ChefData.WorldObject.LocalPosition = m_Transform.localPosition;
			m_ChefData.WorldObject.LocalRotation = m_Transform.localRotation;
			m_ChefData.Velocity = m_Rigidbody.velocity;
			m_ChefData.NetworkTime = ClientTime.Time();
			m_ChefData.ClientTimeStamp = m_InputReceiver.GetLastClientInputTimeStamp();
			ServerMessenger.ResumeChefPositionSync(base.gameObject, m_ChefData, sessionUserId);
			return true;
		}
	}
}
