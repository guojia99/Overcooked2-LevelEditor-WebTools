using System.Collections.Generic;
using GameModes;
using UnityEngine;

namespace Team17.Online.Multiplayer.Messaging
{
	public class ClientSynchronisationReceiver
	{
		private struct ChefToDestroyInfo
		{
			public enum BlockedBy
			{
				None = 0,
				HeldItem = 1,
				Interacting = 2,
				Teleporting = 3
			}

			public IParentable chef;

			public GameObject chefGO;
		}

		private FastList<ChefToDestroyInfo> m_ChefsToDelete = new FastList<ChefToDestroyInfo>();

		private FastList<ClientWorldObjectSynchroniser> m_entitiesToResume = new FastList<ClientWorldObjectSynchroniser>(16);

		private NetworkMessageTracker m_Tracker;

		public void OnEntitySynchronisationMessageReceived(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message, uint uSequence)
		{
			EntitySynchronisationMessage entitySynchronisationMessage = (EntitySynchronisationMessage)message;
			EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(entitySynchronisationMessage.m_Header.m_uEntityID);
			if (entry == null)
			{
				return;
			}
			float num = ClientTime.Time();
			for (int i = 0; i < entry.m_ClientSynchronisedComponents.Count; i++)
			{
				if (entitySynchronisationMessage.m_Payloads._items[i] == null)
				{
					continue;
				}
				bool flag = false;
				ClientSynchroniser clientSynchroniser = entry.m_ClientSynchronisedComponents._items[i];
				if (clientSynchroniser.IsValidServerUpdateSequenceNumber(uSequence))
				{
					flag = true;
				}
				else if (clientSynchroniser.IsValidLastUpdateTimeStamp(num, 15f))
				{
					flag = true;
				}
				if (flag)
				{
					clientSynchroniser.SetLastServerUpdateSequenceNumber(uSequence);
					clientSynchroniser.SetLastUpdateTimeStamp(num);
					clientSynchroniser.ApplyServerUpdate(entitySynchronisationMessage.m_Payloads._items[i]);
					if (m_Tracker != null)
					{
						m_Tracker.TrackReceivedEntityUpdate(clientSynchroniser.GetEntityType());
					}
				}
			}
		}

		public void OnEntityEventMessageReceived(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
		{
			EntityEventMessage entityEventMessage = (EntityEventMessage)message;
			EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(entityEventMessage.m_Header.m_uEntityID);
			if (entry != null)
			{
				ClientSynchroniser clientSynchroniser = entry.m_ClientSynchronisedComponents._items[entityEventMessage.m_ComponentId];
				clientSynchroniser.ApplyServerEvent(entityEventMessage.m_Payload);
				if (m_Tracker != null)
				{
					m_Tracker.TrackReceivedEntityEvent(clientSynchroniser.GetEntityType());
				}
			}
		}

		public void OnSpawnEntityMessageReceived(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
		{
			SpawnEntityMessage spawnEntityMessage = (SpawnEntityMessage)message;
			EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(spawnEntityMessage.m_SpawnerHeader.m_uEntityID);
			if (entry == null || !(null != entry.m_GameObject))
			{
				return;
			}
			INetworkEntitySpawner networkEntitySpawner = entry.m_GameObject.RequireInterface<INetworkEntitySpawner>();
			EntitySerialisationEntry entry2 = EntitySerialisationRegistry.GetEntry(spawnEntityMessage.m_DesiredHeader.m_uEntityID);
			if (entry2 != null)
			{
				return;
			}
			List<VoidGeneric<GameObject>> callbacks = new List<VoidGeneric<GameObject>>();
			GameObject gameObject = networkEntitySpawner.SpawnEntity(spawnEntityMessage.m_SpawnableID, spawnEntityMessage.m_Position, spawnEntityMessage.m_Rotation, ref callbacks);
			EntitySerialisationRegistry.RegisterObject(gameObject, spawnEntityMessage.m_DesiredHeader.m_uEntityID);
			ComponentCacheRegistry.UpdateObject(gameObject);
			if (callbacks == null || callbacks.Count <= 0)
			{
				return;
			}
			for (int i = 0; i < callbacks.Count; i++)
			{
				if (callbacks[i] != null)
				{
					callbacks[i](gameObject);
				}
			}
		}

		public void OnSpawnPhysicalAttachmentMessageReceived(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
		{
			SpawnPhysicalAttachmentMessage spawnPhysicalAttachmentMessage = (SpawnPhysicalAttachmentMessage)message;
			SpawnEntityMessage spawnEntityData = spawnPhysicalAttachmentMessage.m_SpawnEntityData;
			EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(spawnEntityData.m_SpawnerHeader.m_uEntityID);
			if (entry == null || !(null != entry.m_GameObject))
			{
				return;
			}
			INetworkEntitySpawner networkEntitySpawner = entry.m_GameObject.RequireInterface<INetworkEntitySpawner>();
			EntitySerialisationEntry entry2 = EntitySerialisationRegistry.GetEntry(spawnEntityData.m_DesiredHeader.m_uEntityID);
			if (entry2 != null)
			{
				return;
			}
			List<VoidGeneric<GameObject>> callbacks = new List<VoidGeneric<GameObject>>();
			GameObject gameObject = networkEntitySpawner.SpawnEntity(spawnEntityData.m_SpawnableID, spawnEntityData.m_Position, spawnEntityData.m_Rotation, ref callbacks);
			EntitySerialisationRegistry.RegisterObject(gameObject, spawnEntityData.m_DesiredHeader.m_uEntityID);
			ComponentCacheRegistry.UpdateObject(gameObject);
			PhysicalAttachment component = gameObject.GetComponent<PhysicalAttachment>();
			if (null != component)
			{
				EntitySerialisationRegistry.RegisterObject(component.m_container.gameObject, spawnPhysicalAttachmentMessage.m_ContainerHeader.m_uEntityID);
				ComponentCacheRegistry.UpdateObject(component.m_container.gameObject);
			}
			EntitySerialisationRegistry.RegisterObject(gameObject, spawnEntityData.m_DesiredHeader.m_uEntityID);
			ComponentCacheRegistry.UpdateObject(gameObject);
			if (callbacks == null || callbacks.Count <= 0)
			{
				return;
			}
			for (int i = 0; i < callbacks.Count; i++)
			{
				if (callbacks[i] != null)
				{
					callbacks[i](gameObject);
				}
			}
		}

		public void OnDestroyEntityMessageReceived(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
		{
			DestroyEntityMessage destroyEntityMessage = (DestroyEntityMessage)message;
			EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(destroyEntityMessage.m_Header.m_uEntityID);
			if (entry != null && entry.m_GameObject != null)
			{
				DestroyObject(entry.m_GameObject);
			}
		}

		private static void FindSynchroniserEntitiesRecursive(GameObject go, FastList<uint> ids, ref FastList<ClientWorldObjectSynchroniser> list)
		{
			if (go == null)
			{
				return;
			}
			for (int i = 0; i < go.transform.childCount; i++)
			{
				GameObject gameObject = go.transform.GetChild(i).gameObject;
				if (gameObject != null && !ids.Contains(EntitySerialisationRegistry.GetId(gameObject)))
				{
					ClientWorldObjectSynchroniser clientWorldObjectSynchroniser = gameObject.RequestComponent<ClientWorldObjectSynchroniser>();
					if (clientWorldObjectSynchroniser != null)
					{
						list.Add(clientWorldObjectSynchroniser);
					}
				}
				FindSynchroniserEntitiesRecursive(gameObject, ids, ref list);
			}
		}

		public void OnDestroyEntitiesMessageReceived(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
		{
			DestroyEntitiesMessage destroyEntitiesMessage = (DestroyEntitiesMessage)message;
			EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(destroyEntitiesMessage.m_rootId);
			if (entry != null && entry.m_GameObject != null)
			{
				GameObject gameObject = entry.m_GameObject;
				FindSynchroniserEntitiesRecursive(gameObject, destroyEntitiesMessage.m_ids, ref m_entitiesToResume);
				for (int i = 0; i < m_entitiesToResume.Count; i++)
				{
					m_entitiesToResume._items[i].Pause();
					GameObject gameObject2 = m_entitiesToResume._items[i].gameObject;
					gameObject2.transform.SetParent(null);
					ClientMessenger.SendResumeEntitySync(gameObject2);
				}
				DestroyObject(gameObject);
			}
		}

		public void OnDestroyChefMessageReceived(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
		{
			DestroyChefMessage destroyChefMessage = (DestroyChefMessage)message;
			EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(destroyChefMessage.m_Chef.m_Header.m_uEntityID);
			if (entry == null || !(entry.m_GameObject != null))
			{
				return;
			}
			IParentable chef = entry.m_GameObject.RequireInterface<IParentable>();
			int num = CanDeleteChef(chef, entry.m_GameObject);
			if (num == 0)
			{
				DestroyObject(entry.m_GameObject);
				return;
			}
			m_ChefsToDelete.Add(new ChefToDestroyInfo
			{
				chef = chef,
				chefGO = entry.m_GameObject
			});
			if (MaskUtils.HasFlag(num, ChefToDestroyInfo.BlockedBy.Interacting))
			{
				PlayerControls playerControls = entry.m_GameObject.RequestComponent<PlayerControls>();
				IPlayerControlsImpl playerControlsImpl = ((!(playerControls != null)) ? null : playerControls.GetActiveControlsImpl());
				if (playerControls != null)
				{
					playerControls.enabled = false;
				}
			}
		}

		private void DestroyObject(GameObject entity)
		{
			ClientLimitedQuantityItem component = entity.GetComponent<ClientLimitedQuantityItem>();
			if (null != component)
			{
				component.PlayDestructionPFX();
				component.NotifyOfImpendingDestruction();
			}
			EntitySerialisationRegistry.UnregisterObject(entity);
			Object.Destroy(entity);
		}

		private int CanDeleteChef(IParentable chef, GameObject _chefGO)
		{
			int num = 0;
			if (chef != null)
			{
				IPlayerCarrier playerCarrier = _chefGO.RequireInterface<IPlayerCarrier>();
				for (int i = 0; i < 2; i++)
				{
					if (playerCarrier.InspectCarriedItem((PlayerAttachTarget)i) != null || playerCarrier.HasAttachment((PlayerAttachTarget)i))
					{
						num |= 2;
					}
				}
				PlayerControls playerControls = _chefGO.RequestComponent<PlayerControls>();
				if (playerControls != null && playerControls.GetCurrentlyInteracting() != null)
				{
					num |= 4;
				}
			}
			IClientTeleportable clientTeleportable = _chefGO.RequestInterface<IClientTeleportable>();
			if (clientTeleportable != null && clientTeleportable.IsTeleporting())
			{
				num |= 8;
			}
			return num;
		}

		public void Update()
		{
			for (int num = m_ChefsToDelete.Count - 1; num >= 0; num--)
			{
				ChefToDestroyInfo item = m_ChefsToDelete._items[num];
				if (CanDeleteChef(item.chef, item.chefGO) == 0)
				{
					m_ChefsToDelete.Remove(item);
					DestroyObject(item.chefGO);
				}
			}
			for (int num2 = m_entitiesToResume.Count - 1; num2 >= 0; num2--)
			{
				ClientWorldObjectSynchroniser clientWorldObjectSynchroniser = m_entitiesToResume._items[num2];
				if (clientWorldObjectSynchroniser != null && clientWorldObjectSynchroniser.IsReadyToResume())
				{
					clientWorldObjectSynchroniser.Resume();
					m_entitiesToResume.RemoveAt(num2);
				}
				else if (clientWorldObjectSynchroniser == null)
				{
					m_entitiesToResume.RemoveAt(num2);
				}
			}
		}

		public void OnResumeObjectMessageReceived<T>(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message) where T : Serialisable, new()
		{
			ResumeObjectSyncMessage<T> resumeObjectSyncMessage = (ResumeObjectSyncMessage<T>)message;
			EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(resumeObjectSyncMessage.EntityID);
			if (entry != null && null != entry.m_GameObject)
			{
				ClientWorldObjectSynchroniser clientWorldObjectSynchroniser = entry.m_GameObject.RequireComponent<ClientWorldObjectSynchroniser>();
				clientWorldObjectSynchroniser.OnResumeDataReceived(resumeObjectSyncMessage.Data);
			}
		}

		public void OnTriggerAudioMessageReceived(IOnlineMultiplayerSessionUserId _sender, Serialisable _message)
		{
			TriggerAudioMessage triggerAudioMessage = _message as TriggerAudioMessage;
			AudioManager audioManager = GameUtils.RequireManager<AudioManager>();
			audioManager.TriggerAudio(triggerAudioMessage.AudioTag, triggerAudioMessage.Layer);
		}

		public void OnSessionConfigReceived(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
		{
			SessionConfigSyncMessage sessionConfigSyncMessage = (SessionConfigSyncMessage)message;
			GameSession gameSession = GameUtils.GetGameSession();
			gameSession.GameModeSessionConfig = sessionConfigSyncMessage.m_config;
		}

		public void SetTracker(NetworkMessageTracker tracker)
		{
			m_Tracker = tracker;
		}

		public void CleanUp()
		{
			m_ChefsToDelete.Clear();
		}
	}
}
