using System;
using System.Collections.Generic;
using UnityEngine;

namespace Team17.Online.Multiplayer.Messaging
{
	public class ServerSynchronisationScheduler
	{
		private const float kFrameRate = 10f;

		private const float kFrameDelay = 0.1f;

		private const float kFastFrameRate = 30f;

		private const float kFastFrameDelay = 1f / 30f;

		private float m_fNextUpdate;

		private float m_fNextFastUpdate;

		private FastList<EntitySerialisationEntry> m_EntitiesList;

		private FastList<EntitySerialisationEntry> m_FastEntitiesList;

		private IOnlineMultiplayerSessionCoordinator m_SessionCoordinator;

		private NetworkMessageTracker m_Tracker;

		private bool m_bStarted;

		private FastList<Serialisable> m_GlobalPayloadCache = new FastList<Serialisable>(16);

		private FastList<Serialisable> m_TargetPayloadCache = new FastList<Serialisable>(16);

		public void Initialise()
		{
			m_EntitiesList = new FastList<EntitySerialisationEntry>();
			m_FastEntitiesList = new FastList<EntitySerialisationEntry>();
			IOnlinePlatformManager onlinePlatformManager = GameUtils.RequireManagerInterface<IOnlinePlatformManager>();
			m_SessionCoordinator = onlinePlatformManager.OnlineMultiplayerSessionCoordinator();
		}

		public void AddToFastList(EntitySerialisationEntry entry)
		{
			m_FastEntitiesList.Add(entry);
			if (m_EntitiesList.Contains(entry))
			{
				m_EntitiesList.Remove(entry);
			}
		}

		public void StartSynchronising()
		{
			EntitySerialisationRegistry.OnEntryAdded = (GenericVoid<EntitySerialisationEntry>)Delegate.Combine(EntitySerialisationRegistry.OnEntryAdded, new GenericVoid<EntitySerialisationEntry>(OnEntryAdded));
			EntitySerialisationRegistry.OnEntryRemoved = (GenericVoid<EntitySerialisationEntry>)Delegate.Combine(EntitySerialisationRegistry.OnEntryRemoved, new GenericVoid<EntitySerialisationEntry>(OnEntryRemoved));
			DisconnectionHandler.SessionConnectionLostEvent = (GenericVoid)Delegate.Combine(DisconnectionHandler.SessionConnectionLostEvent, new GenericVoid(OnNetworkError));
			DisconnectionHandler.ConnectionModeErrorEvent = (GenericVoid<OnlineMultiplayerReturnCode<OnlineMultiplayerConnectionModeErrorResult>>)Delegate.Combine(DisconnectionHandler.ConnectionModeErrorEvent, new GenericVoid<OnlineMultiplayerReturnCode<OnlineMultiplayerConnectionModeErrorResult>>(OnConnectionModeError));
			DisconnectionHandler.KickedFromSessionEvent = (GenericVoid)Delegate.Combine(DisconnectionHandler.KickedFromSessionEvent, new GenericVoid(OnNetworkError));
			DisconnectionHandler.LocalDisconnectionEvent = (GenericVoid<OnlineMultiplayerReturnCode<OnlineMultiplayerSessionDisconnectionResult>>)Delegate.Combine(DisconnectionHandler.LocalDisconnectionEvent, new GenericVoid<OnlineMultiplayerReturnCode<OnlineMultiplayerSessionDisconnectionResult>>(OnLocalDisconnection));
			BuildEntityLists();
			m_bStarted = true;
		}

		public void StopSynchronising()
		{
			m_bStarted = false;
			m_EntitiesList = null;
			m_FastEntitiesList = null;
			DisconnectionHandler.SessionConnectionLostEvent = (GenericVoid)Delegate.Remove(DisconnectionHandler.SessionConnectionLostEvent, new GenericVoid(OnNetworkError));
			DisconnectionHandler.ConnectionModeErrorEvent = (GenericVoid<OnlineMultiplayerReturnCode<OnlineMultiplayerConnectionModeErrorResult>>)Delegate.Remove(DisconnectionHandler.ConnectionModeErrorEvent, new GenericVoid<OnlineMultiplayerReturnCode<OnlineMultiplayerConnectionModeErrorResult>>(OnConnectionModeError));
			DisconnectionHandler.KickedFromSessionEvent = (GenericVoid)Delegate.Remove(DisconnectionHandler.KickedFromSessionEvent, new GenericVoid(OnNetworkError));
			DisconnectionHandler.LocalDisconnectionEvent = (GenericVoid<OnlineMultiplayerReturnCode<OnlineMultiplayerSessionDisconnectionResult>>)Delegate.Remove(DisconnectionHandler.LocalDisconnectionEvent, new GenericVoid<OnlineMultiplayerReturnCode<OnlineMultiplayerSessionDisconnectionResult>>(OnLocalDisconnection));
			EntitySerialisationRegistry.OnEntryAdded = (GenericVoid<EntitySerialisationEntry>)Delegate.Remove(EntitySerialisationRegistry.OnEntryAdded, new GenericVoid<EntitySerialisationEntry>(OnEntryAdded));
			EntitySerialisationRegistry.OnEntryRemoved = (GenericVoid<EntitySerialisationEntry>)Delegate.Remove(EntitySerialisationRegistry.OnEntryRemoved, new GenericVoid<EntitySerialisationEntry>(OnEntryRemoved));
		}

		private void OnEntryAdded(EntitySerialisationEntry entry)
		{
			if (!m_EntitiesList.Contains(entry) && !m_FastEntitiesList.Contains(entry))
			{
				m_EntitiesList.Add(entry);
			}
		}

		private void OnEntryRemoved(EntitySerialisationEntry entry)
		{
			m_EntitiesList.Remove(entry);
			m_FastEntitiesList.Remove(entry);
		}

		private void OnNetworkError()
		{
			StopSynchronising();
		}

		private void OnConnectionModeError(OnlineMultiplayerReturnCode<OnlineMultiplayerConnectionModeErrorResult> result)
		{
			StopSynchronising();
		}

		private void OnLocalDisconnection(OnlineMultiplayerReturnCode<OnlineMultiplayerSessionDisconnectionResult> result)
		{
			StopSynchronising();
		}

		public void Update()
		{
			if (m_bStarted)
			{
				float deltaTime = Time.deltaTime;
				m_fNextUpdate += deltaTime;
				m_fNextFastUpdate += deltaTime;
				SynchroniseList(m_EntitiesList, 0.1f, ref m_fNextUpdate);
				if (DebugManager.Instance.GetOption("Fast NetworkChefs"))
				{
					SynchroniseList(m_FastEntitiesList, 1f / 30f, ref m_fNextFastUpdate);
				}
				else
				{
					SynchroniseList(m_FastEntitiesList, 0.1f, ref m_fNextFastUpdate);
				}
				EntitySerialisationRegistry.HasUrgentOutgoingUpdates = false;
			}
		}

		private void SynchroniseList(FastList<EntitySerialisationEntry> entities, float fFrameDelay, ref float fNextUpdate)
		{
			if (entities == null)
			{
				return;
			}
			if (fNextUpdate >= fFrameDelay)
			{
				int count = entities.Count;
				for (int i = 0; i < count; i++)
				{
					EntitySerialisationEntry entitySerialisationEntry = entities._items[i];
					SynchroniseEntity(entitySerialisationEntry);
					entitySerialisationEntry.SetRequiresUrgentUpdate(false);
				}
				fNextUpdate -= fFrameDelay;
			}
			else
			{
				if (!EntitySerialisationRegistry.HasUrgentOutgoingUpdates)
				{
					return;
				}
				int count2 = entities.Count;
				for (int j = 0; j < count2; j++)
				{
					EntitySerialisationEntry entitySerialisationEntry2 = entities._items[j];
					if (entitySerialisationEntry2.HasUrgentUpdate())
					{
						SynchroniseEntity(entitySerialisationEntry2);
						entitySerialisationEntry2.SetRequiresUrgentUpdate(false);
					}
				}
			}
		}

		private void BuildEntityLists()
		{
			FastList<EntitySerialisationEntry> entitiesList = EntitySerialisationRegistry.m_EntitiesList;
			EntitySerialisationEntry entitySerialisationEntry = null;
			for (int i = 0; i < entitiesList.Count; i++)
			{
				entitySerialisationEntry = entitiesList._items[i];
				if (!m_EntitiesList.Contains(entitySerialisationEntry) && !m_FastEntitiesList.Contains(entitySerialisationEntry))
				{
					m_EntitiesList.Add(entitySerialisationEntry);
				}
			}
		}

		private void SynchroniseEntity(EntitySerialisationEntry entry)
		{
			bool flag = false;
			int num = 0;
			m_GlobalPayloadCache.Clear();
			for (int i = 0; i < entry.m_ServerSynchronisedComponents.Count; i++)
			{
				ServerSynchroniser serverSynchroniser = entry.m_ServerSynchronisedComponents._items[i];
				flag |= serverSynchroniser.HasTargetedServerUpdates();
				Serialisable serverUpdate = serverSynchroniser.GetServerUpdate();
				if (serverUpdate != null)
				{
					if (m_Tracker != null)
					{
						m_Tracker.TrackSentEntityUpdate(serverSynchroniser.GetEntityType());
					}
					num++;
				}
				m_GlobalPayloadCache.Add(serverUpdate);
			}
			if (!flag && num > 0)
			{
				ServerMessenger.EntitySynchronisation(entry.m_Header, m_GlobalPayloadCache);
			}
			if (!flag)
			{
				return;
			}
			SynchroniseForRecipient(entry, m_GlobalPayloadCache, null);
			if (m_SessionCoordinator == null)
			{
				return;
			}
			IOnlineMultiplayerSessionUserId[] array = m_SessionCoordinator.Members();
			if (array == null)
			{
				return;
			}
			foreach (IOnlineMultiplayerSessionUserId onlineMultiplayerSessionUserId in array)
			{
				if (onlineMultiplayerSessionUserId != null && !onlineMultiplayerSessionUserId.IsHost)
				{
					SynchroniseForRecipient(entry, m_GlobalPayloadCache, onlineMultiplayerSessionUserId);
				}
			}
		}

		private void SynchroniseForRecipient(EntitySerialisationEntry entry, FastList<Serialisable> globalPayloads, IOnlineMultiplayerSessionUserId currentMember)
		{
			m_TargetPayloadCache.Clear();
			int num = 0;
			for (int i = 0; i < entry.m_ServerSynchronisedComponents.Count; i++)
			{
				ServerSynchroniser serverSynchroniser = entry.m_ServerSynchronisedComponents._items[i];
				Serialisable serverUpdateForRecipient = serverSynchroniser.GetServerUpdateForRecipient(currentMember);
				if (serverUpdateForRecipient != null)
				{
					if (m_Tracker != null)
					{
						m_Tracker.TrackSentEntityUpdate(serverSynchroniser.GetEntityType());
					}
					num++;
				}
				if (serverUpdateForRecipient != null)
				{
					m_TargetPayloadCache.Add(serverUpdateForRecipient);
				}
				else
				{
					m_TargetPayloadCache.Add(globalPayloads._items[i]);
				}
			}
			if (num > 0)
			{
				ServerMessenger.EntitySynchronisation(entry.m_Header, currentMember, m_TargetPayloadCache);
			}
		}

		public void SetTracker(NetworkMessageTracker tracker)
		{
			m_Tracker = tracker;
		}
	}
}
