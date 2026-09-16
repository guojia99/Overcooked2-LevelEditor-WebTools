using System;
using System.Collections.Generic;
using UnityEngine;

namespace Team17.Online.Multiplayer.Messaging
{
	public class ServerWorldObjectSynchroniser : ServerSynchroniserBase
	{
		public const float m_TimeUntilReliableUpdate = 1f;

		private WorldObjectMessage m_ServerData = new WorldObjectMessage();

		protected Transform m_Transform;

		private Transform m_CachedParentTransform;

		private float m_LastUnreliableActiveSend;

		private bool m_bSentReliableRestPosition;

		protected bool m_bStartedSynchronising;

		protected bool m_bSleepAllowed = true;

		private bool m_bActive;

		private bool m_bSyncPositions = true;

		private bool m_bParentChanged = true;

		protected bool m_bPaused;

		private IOnlineMultiplayerSessionCoordinator m_SessionCoordinator;

		protected WorldObjectMessage GetMessageData()
		{
			return m_ServerData;
		}

		public virtual void Awake()
		{
			m_Transform = base.transform;
			IOnlinePlatformManager onlinePlatformManager = GameUtils.RequireManagerInterface<IOnlinePlatformManager>();
			m_SessionCoordinator = onlinePlatformManager.OnlineMultiplayerSessionCoordinator();
		}

		public void PausePositions()
		{
			m_bSyncPositions = false;
		}

		public void ResumePositions()
		{
			m_bSyncPositions = true;
			m_bSentReliableRestPosition = false;
			m_LastUnreliableActiveSend = 0f;
			RefreshParent();
		}

		public override void StartSynchronising(Component synchronisedObject)
		{
			m_Transform = base.transform;
			if (m_Transform.parent != null)
			{
				IParentable parentable = m_Transform.parent.gameObject.RequestInterfaceUpwardsRecursive<IParentable>();
				if (parentable != null)
				{
					m_Transform.SetParent(parentable.GetAttachPoint(base.gameObject), true);
				}
			}
			RefreshParent();
			UserSystemUtils.OnServerChangedGameState = (GenericVoid<GameState, GameStateMessage.GameStatePayload>)Delegate.Combine(UserSystemUtils.OnServerChangedGameState, new GenericVoid<GameState, GameStateMessage.GameStatePayload>(OnGameStateChanged));
			FastList<User> users = ServerUserSystem.m_Users;
			User.MachineID s_LocalMachineId = ServerUserSystem.s_LocalMachineId;
			User user = UserSystemUtils.FindUser(users, null, s_LocalMachineId, EngagementSlot.One);
			if (user != null)
			{
				switch (user.GameState)
				{
				case GameState.LoadKitchen:
				case GameState.LoadedKitchen:
				case GameState.ScanNetworkEntities:
				case GameState.ScannedNetworkEntities:
				case GameState.StartSynchronising:
				case GameState.StartedSyncronising:
				case GameState.AssignChefsToUsers:
				case GameState.AssignedChefsToUsers:
				case GameState.StartEntities:
				case GameState.StartedEntities:
				case GameState.LoadMap:
				case GameState.LoadedMap:
				case GameState.MapScanNetworkEntities:
				case GameState.MapScannedNetworkEntities:
				case GameState.MapStartSynchronising:
				case GameState.MapStartedSyncronising:
				case GameState.MapStartEntities:
				case GameState.MapStartedEntities:
					m_bSleepAllowed = false;
					break;
				}
			}
			m_bStartedSynchronising = true;
			m_bSyncPositions = true;
		}

		public override void StopSynchronising()
		{
			UserSystemUtils.OnServerChangedGameState = (GenericVoid<GameState, GameStateMessage.GameStatePayload>)Delegate.Remove(UserSystemUtils.OnServerChangedGameState, new GenericVoid<GameState, GameStateMessage.GameStatePayload>(OnGameStateChanged));
		}

		protected virtual void OnGameStateChanged(GameState state, GameStateMessage.GameStatePayload payload)
		{
			switch (state)
			{
			case GameState.StartSynchronising:
			case GameState.MapStartSynchronising:
				m_bSleepAllowed = false;
				break;
			case GameState.RunKitchen:
			case GameState.RunMapUnfoldRoutine:
				m_bSleepAllowed = true;
				break;
			}
		}

		public override EntityType GetEntityType()
		{
			return EntityType.WorldObject;
		}

		public override Serialisable GetServerUpdate()
		{
			if (!m_bStartedSynchronising)
			{
				return null;
			}
			if (null == m_Transform)
			{
				return null;
			}
			m_bActive = false;
			if (!m_bSleepAllowed)
			{
				m_bActive = true;
			}
			m_bActive |= PopulateMessage();
			if (m_bActive)
			{
				m_bSentReliableRestPosition = false;
				m_LastUnreliableActiveSend = Time.time;
				return m_ServerData;
			}
			if (m_bSleepAllowed && !m_bSentReliableRestPosition && Time.time > m_LastUnreliableActiveSend + 1f)
			{
				m_bSentReliableRestPosition = true;
				m_bParentChanged = false;
				SendServerEvent(m_ServerData);
				return null;
			}
			if (m_bParentChanged)
			{
				return m_ServerData;
			}
			return null;
		}

		protected bool IsWorldObjectActive()
		{
			return m_bActive;
		}

		private bool PopulateMessage()
		{
			bool flag = false;
			bool flag2 = m_bSyncPositions;
			if (m_CachedParentTransform != m_Transform.parent)
			{
				flag = true;
				m_bParentChanged = true;
				RefreshParent();
			}
			if (m_bParentChanged)
			{
				flag2 = true;
			}
			m_ServerData.HasPositions = flag2;
			bool flag3 = (m_ServerData.LocalPosition - m_Transform.localPosition).sqrMagnitude > 0.001f;
			bool flag4 = Quaternion.Dot(m_ServerData.LocalRotation, m_Transform.localRotation) < 0.99f;
			if (flag || (flag2 && (flag3 || flag4)))
			{
				flag |= m_bSyncPositions;
				m_ServerData.LocalPosition = m_Transform.localPosition;
				m_ServerData.LocalRotation = m_Transform.localRotation;
			}
			else if (m_bParentChanged)
			{
				m_ServerData.HasPositions = true;
				m_ServerData.LocalPosition = m_Transform.localPosition;
				m_ServerData.LocalRotation = m_Transform.localRotation;
			}
			return flag;
		}

		protected void RefreshParent()
		{
			Transform parent = m_Transform.parent;
			if (m_CachedParentTransform != parent)
			{
				m_bParentChanged = true;
			}
			m_CachedParentTransform = parent;
			m_ServerData.HasParent = m_CachedParentTransform != null;
			m_ServerData.ParentEntityID = 0u;
			if (!(m_CachedParentTransform != null))
			{
				return;
			}
			IParentable parentable = m_CachedParentTransform.gameObject.RequestInterfaceUpwardsRecursive<IParentable>();
			if (parentable != null)
			{
				MonoBehaviour monoBehaviour = parentable as MonoBehaviour;
				GameObject gameObject = monoBehaviour.gameObject;
				EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(gameObject);
				if (entry != null)
				{
					m_ServerData.ParentEntityID = entry.m_Header.m_uEntityID;
				}
			}
		}

		public void ResumeAllClients(bool _resumeServerClient = true)
		{
			bool flag = true;
			if (m_SessionCoordinator != null)
			{
				IOnlineMultiplayerSessionUserId[] array = m_SessionCoordinator.Members();
				if (array != null)
				{
					for (int i = 0; i < array.Length; i++)
					{
						flag &= SendResumeData(array[i]);
					}
				}
			}
			if (_resumeServerClient)
			{
				flag &= SendResumeData(null);
			}
			if (flag)
			{
				m_bPaused = false;
			}
		}

		public void ResumeClient(IOnlineMultiplayerSessionUserId sessionUserId)
		{
			if (SendResumeData(sessionUserId))
			{
				m_bPaused = false;
			}
		}

		protected virtual bool SendResumeData(IOnlineMultiplayerSessionUserId sessionUserId)
		{
			RefreshParent();
			m_ServerData.LocalPosition = m_Transform.localPosition;
			m_ServerData.LocalRotation = m_Transform.localRotation;
			ServerMessenger.ResumeWorldObjectSync(base.gameObject, m_ServerData, sessionUserId);
			return true;
		}
	}
}
