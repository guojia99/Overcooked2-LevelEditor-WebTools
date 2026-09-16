using System.Collections.Generic;
using GameModes;
using Team17.Online;
using Team17.Online.Multiplayer;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

internal class ServerMessenger
{
	private enum DeferredLoadLevelState
	{
		eInvalid = 0,
		eLoadByIndex = 1,
		eLoadByName = 2
	}

	private static DeferredLoadLevelState m_deferredLoadLevelState = DeferredLoadLevelState.eInvalid;

	private static LevelLoadByIndexMessage m_LevelLoadByIndex = new LevelLoadByIndexMessage();

	private static LevelLoadByNameMessage m_LevelLoadByName = new LevelLoadByNameMessage();

	private static ExampleNetworkMessage m_Example = new ExampleNetworkMessage();

	private static DestroyEntityMessage m_DestroyEntity = new DestroyEntityMessage();

	private static DestroyEntitiesMessage m_destroyEntities = new DestroyEntitiesMessage();

	private static FastList<uint> m_destroyEntityIds = new FastList<uint>(16);

	private static DestroyChefMessage m_DestroyChef = new DestroyChefMessage();

	private static SpawnPhysicalAttachmentMessage m_SpawnPhysicalAttachment = new SpawnPhysicalAttachmentMessage();

	private static SpawnEntityMessage m_SpawnEntity = new SpawnEntityMessage();

	private static EntitySynchronisationMessage m_EntitySynchronisation = new EntitySynchronisationMessage();

	private static EntityEventMessage m_EntityEvent = new EntityEventMessage();

	private static UsersChangedMessage m_UsersChanged = new UsersChangedMessage();

	private static UserAddedMessage m_UserAdded = new UserAddedMessage();

	private static UsersChangedMessage m_ChefOwnership = new UsersChangedMessage();

	private static GameStateMessage m_GameState = new GameStateMessage();

	private static ChefAvatarMessage m_ChefAvatar = new ChefAvatarMessage();

	private static TimeSyncMessage m_TimeSyncMessage = new TimeSyncMessage();

	private static GameSetupMessage m_GameSetupMessage = new GameSetupMessage();

	private static GameProgressDataNetworkMessage m_GameProgressDataMessage = new GameProgressDataNetworkMessage();

	private static SetupCoopSessionNetworkMessage m_SetupCoopSessionDataMessage = new SetupCoopSessionNetworkMessage();

	private static AchievementMessage m_AchievementMessage = new AchievementMessage();

	private static ChefEffectMessage m_ChefEffectMessage = new ChefEffectMessage();

	private static TriggerAudioMessage m_triggerAudioMessage = new TriggerAudioMessage();

	private static SessionConfigSyncMessage m_hostModeConfigChangedMessage = new SessionConfigSyncMessage();

	private static Server m_LocalServer = null;

	public static void DeferredLevelLoad()
	{
		if (m_LocalServer != null)
		{
			switch (m_deferredLoadLevelState)
			{
			case DeferredLoadLevelState.eLoadByIndex:
				m_LocalServer.BroadcastMessageToAll(MessageType.LevelLoadByIndex, m_LevelLoadByIndex);
				m_deferredLoadLevelState = DeferredLoadLevelState.eInvalid;
				break;
			case DeferredLoadLevelState.eLoadByName:
				m_LocalServer.BroadcastMessageToAll(MessageType.LevelLoadByName, m_LevelLoadByName);
				m_deferredLoadLevelState = DeferredLoadLevelState.eInvalid;
				break;
			}
		}
	}

	public static void LoadLevel(uint uLevelIndex, uint players, GameState setAtLoadingBegin, GameState waitForHide = global::GameState.NotSet)
	{
		ServerUserSystem.LockEngagement();
		InviteMonitor.SwitchHandlerType(InviteMonitor.HandlerType.None);
		m_LevelLoadByIndex.Initialise(setAtLoadingBegin, waitForHide, uLevelIndex, players, true);
		if (m_LocalServer != null)
		{
			m_LocalServer.BroadcastMessageToAll(MessageType.LevelLoadByIndex, m_LevelLoadByIndex);
			m_deferredLoadLevelState = DeferredLoadLevelState.eInvalid;
		}
		else
		{
			m_deferredLoadLevelState = DeferredLoadLevelState.eLoadByIndex;
		}
	}

	public static void LoadLevel(string sceneName, GameState setAtLoadingBegin, bool bUseLoadingScreen, GameState waitForHide = global::GameState.NotSet)
	{
		int num = ClientUserSystem.m_Users.Count;
		ServerUserSystem.LockEngagement();
		GameSession gameSession = GameUtils.GetGameSession();
		InviteMonitor.SwitchHandlerType(InviteMonitor.HandlerType.None);
		if (gameSession != null && gameSession.TypeSettings.Type == GameSession.GameType.Competitive)
		{
			num = 4;
		}
		if (null != gameSession && null != gameSession.Progress)
		{
			SceneDirectoryData sceneDirectory = gameSession.Progress.GetSceneDirectory();
			if (null != sceneDirectory && sceneDirectory.Scenes != null)
			{
				for (int i = 0; i < sceneDirectory.Scenes.Length; i++)
				{
					SceneDirectoryData.SceneDirectoryEntry sceneDirectoryEntry = sceneDirectory.Scenes[i];
					SceneDirectoryData.PerPlayerCountDirectoryEntry sceneVarient = sceneDirectoryEntry.GetSceneVarient(num);
					if (sceneVarient != null && sceneVarient.SceneName.Equals(sceneName))
					{
						m_LevelLoadByIndex.Initialise(setAtLoadingBegin, waitForHide, (uint)i, (uint)num, bUseLoadingScreen);
						if (m_LocalServer != null)
						{
							m_LocalServer.BroadcastMessageToAll(MessageType.LevelLoadByIndex, m_LevelLoadByIndex);
							m_deferredLoadLevelState = DeferredLoadLevelState.eInvalid;
						}
						else
						{
							m_deferredLoadLevelState = DeferredLoadLevelState.eLoadByIndex;
						}
						return;
					}
				}
			}
		}
		m_LevelLoadByName.Initialise(setAtLoadingBegin, waitForHide, sceneName, bUseLoadingScreen);
		if (m_LocalServer != null)
		{
			m_LocalServer.BroadcastMessageToAll(MessageType.LevelLoadByName, m_LevelLoadByName);
			m_deferredLoadLevelState = DeferredLoadLevelState.eInvalid;
		}
		else
		{
			m_deferredLoadLevelState = DeferredLoadLevelState.eLoadByName;
		}
	}

	public static bool Example(float fFloat, bool bBool)
	{
		if (m_LocalServer != null)
		{
			m_Example.Initialise(fFloat, bBool);
			m_LocalServer.BroadcastMessageToAll(MessageType.Example, m_Example);
			return true;
		}
		return false;
	}

	public static bool DestroyEntity(GameObject gameObject)
	{
		if (m_LocalServer != null)
		{
			EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(gameObject);
			if (entry != null)
			{
				m_DestroyEntity.Initialise(entry.m_Header);
				m_LocalServer.BroadcastMessageToAll(MessageType.DestroyEntity, m_DestroyEntity);
			}
			return true;
		}
		return false;
	}

	private static void FindNetworkedEntitiesRecursive(GameObject root, ref FastList<uint> ids)
	{
		if (root == null)
		{
			return;
		}
		for (int i = 0; i < root.transform.childCount; i++)
		{
			GameObject gameObject = root.transform.GetChild(i).gameObject;
			if (gameObject != null)
			{
				uint id = EntitySerialisationRegistry.GetId(gameObject);
				if (id != 0)
				{
					ids.Add(id);
				}
			}
			FindNetworkedEntitiesRecursive(gameObject, ref ids);
		}
	}

	public static bool DestroyEntities(GameObject root)
	{
		if (m_LocalServer != null)
		{
			uint id = EntitySerialisationRegistry.GetId(root);
			m_destroyEntityIds.Clear();
			FindNetworkedEntitiesRecursive(root, ref m_destroyEntityIds);
			m_destroyEntities.Initialise(id, m_destroyEntityIds);
			m_LocalServer.BroadcastMessageToAll(MessageType.DestroyEntities, m_destroyEntities);
			return true;
		}
		return false;
	}

	public static bool DestroyChef(GameObject gameObject)
	{
		if (m_LocalServer != null)
		{
			EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(gameObject);
			if (entry != null)
			{
				m_DestroyChef.Initialise(entry.m_Header);
				m_LocalServer.BroadcastMessageToAll(MessageType.DestroyChef, m_DestroyChef);
			}
			return true;
		}
		return false;
	}

	public static bool SpawnPhysicalAttachment(INetworkEntitySpawner _spawner, int _spawnableID, EntityMessageHeader _desiredHeader, Vector3 _position, Quaternion _rotation, EntityMessageHeader _containerHeader)
	{
		if (m_LocalServer != null)
		{
			EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(_spawner.AccessGameObject());
			if (entry != null)
			{
				m_SpawnPhysicalAttachment.Initialise(entry.m_Header, _spawnableID, _desiredHeader, _position, _rotation, _containerHeader);
				m_LocalServer.BroadcastMessageToAll(MessageType.SpawnPhysicalAttachment, m_SpawnPhysicalAttachment);
			}
			return true;
		}
		return false;
	}

	public static bool SpawnEntity(INetworkEntitySpawner _spawner, int _spawnableID, EntityMessageHeader _desiredHeader, Vector3 _position, Quaternion _rotation)
	{
		if (m_LocalServer != null)
		{
			EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(_spawner.AccessGameObject());
			if (entry != null)
			{
				m_SpawnEntity.Initialise(entry.m_Header, _spawnableID, _desiredHeader, _position, _rotation);
				m_LocalServer.BroadcastMessageToAll(MessageType.SpawnEntity, m_SpawnEntity);
			}
			return true;
		}
		return false;
	}

	private static bool ResumeObjectSync<T>(MessageType _messageType, GameObject _object, T _resumeData, IOnlineMultiplayerSessionUserId sessionUserId) where T : Serialisable, new()
	{
		if (m_LocalServer != null)
		{
			EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(_object);
			if (entry != null)
			{
				ResumeObjectSyncMessage<T> resumeObjectSyncMessage = new ResumeObjectSyncMessage<T>();
				resumeObjectSyncMessage.Initialise(entry.m_Header.m_uEntityID, _resumeData);
				m_LocalServer.SendMessageToClient(sessionUserId, _messageType, resumeObjectSyncMessage);
			}
			return true;
		}
		return false;
	}

	public static bool ResumeWorldObjectSync(GameObject _object, WorldObjectMessage _data, IOnlineMultiplayerSessionUserId sessionUserId)
	{
		return ResumeObjectSync(MessageType.ResumeWorldObjectSync, _object, _data, sessionUserId);
	}

	public static bool ResumeChefPositionSync(GameObject _object, ChefPositionMessage _data, IOnlineMultiplayerSessionUserId sessionUserId)
	{
		return ResumeObjectSync(MessageType.ResumeChefPositionSync, _object, _data, sessionUserId);
	}

	public static bool ResumePhysicsObjectSync(GameObject _object, PhysicsObjectMessage _data, IOnlineMultiplayerSessionUserId sessionUserId)
	{
		return ResumeObjectSync(MessageType.ResumePhysicsObjectSync, _object, _data, sessionUserId);
	}

	public static bool EntitySynchronisation(EntityMessageHeader header, FastList<Serialisable> payloads)
	{
		if (m_LocalServer != null)
		{
			m_EntitySynchronisation.Initialise(header, payloads);
			m_LocalServer.BroadcastMessageToAll(MessageType.EntitySynchronisation, m_EntitySynchronisation, false);
			return true;
		}
		return false;
	}

	public static bool EntitySynchronisation(EntityMessageHeader header, IOnlineMultiplayerSessionUserId recipient, FastList<Serialisable> payloads)
	{
		if (m_LocalServer != null)
		{
			m_EntitySynchronisation.Initialise(header, payloads);
			m_LocalServer.SendMessageToClient(recipient, MessageType.EntitySynchronisation, m_EntitySynchronisation, false);
			return true;
		}
		return false;
	}

	public static bool EntityEvent(ServerSynchroniserBase synchroniser, Serialisable payload)
	{
		if (m_LocalServer != null)
		{
			EntityMessageHeader entityMessageHeader = new EntityMessageHeader();
			entityMessageHeader.m_uEntityID = synchroniser.GetEntityId();
			NetworkMessageTracker tracker = m_LocalServer.GetTracker();
			if (tracker != null)
			{
				tracker.TrackSentEntityEvent(synchroniser.GetEntityType());
			}
			m_EntityEvent.Initialise(entityMessageHeader, synchroniser.GetComponentId(), payload);
			m_LocalServer.BroadcastMessageToAll(MessageType.EntityEvent, m_EntityEvent);
			return true;
		}
		return false;
	}

	public static bool EntityEvent(IOnlineMultiplayerSessionUserId recipient, ServerSynchroniserBase synchroniser, Serialisable payload)
	{
		if (m_LocalServer != null)
		{
			EntityMessageHeader entityMessageHeader = new EntityMessageHeader();
			entityMessageHeader.m_uEntityID = synchroniser.GetEntityId();
			NetworkMessageTracker tracker = m_LocalServer.GetTracker();
			if (tracker != null)
			{
				tracker.TrackSentEntityEvent(synchroniser.GetEntityType());
			}
			m_EntityEvent.Initialise(entityMessageHeader, synchroniser.GetComponentId(), payload);
			m_LocalServer.SendMessageToClient(recipient, MessageType.EntityEvent, m_EntityEvent);
			return true;
		}
		return false;
	}

	public static bool UsersChanged()
	{
		if (m_LocalServer != null)
		{
			m_UsersChanged.Initialise(ServerUserSystem.m_Users);
			m_LocalServer.BroadcastMessageToAll(MessageType.UsersChanged, m_UsersChanged);
			return true;
		}
		return false;
	}

	public static bool UserAdded(uint _idx, User _user)
	{
		if (m_LocalServer != null)
		{
			m_UserAdded.Initialise(_idx, _user);
			m_LocalServer.BroadcastMessageToAll(MessageType.UsersAdded, m_UserAdded);
			return true;
		}
		return false;
	}

	public static bool ChefOwnership()
	{
		if (m_LocalServer != null)
		{
			m_ChefOwnership.Initialise(ServerUserSystem.m_Users);
			m_LocalServer.BroadcastMessageToAll(MessageType.ChefOwnership, m_ChefOwnership);
			return true;
		}
		return false;
	}

	public static bool GameState(GameState state, GameStateMessage.GameStatePayload payload = null)
	{
		m_GameState.Initialise(state, ServerUserSystem.s_LocalMachineId, payload);
		if (m_LocalServer != null)
		{
			m_LocalServer.BroadcastMessageToAll(MessageType.GameState, m_GameState);
			return true;
		}
		return false;
	}

	public static bool ChefAvatar(uint chefAvatar, User user)
	{
		if (m_LocalServer != null)
		{
			m_ChefAvatar.Initialise(chefAvatar, user.Machine, user.Engagement, user.Split);
			m_LocalServer.BroadcastMessageToAll(MessageType.ChefAvatar, m_ChefAvatar);
			return true;
		}
		return false;
	}

	public static bool LobbyMessage(LobbyServerMessage _message)
	{
		if (m_LocalServer != null)
		{
			m_LocalServer.BroadcastMessageToAll(MessageType.LobbyServer, _message, _message.ToSendReliable());
			return true;
		}
		return false;
	}

	public static bool EmoteWheelMessage(EmoteWheelMessage _message)
	{
		if (m_LocalServer != null)
		{
			m_LocalServer.BroadcastMessageToAll(MessageType.EmoteWheel, _message);
			return true;
		}
		return false;
	}

	public static bool DynamicLevelMessage(DynamicLevelMessage _message)
	{
		if (m_LocalServer != null)
		{
			m_LocalServer.BroadcastMessageToAll(MessageType.DynamicLevel, _message);
			return true;
		}
		return false;
	}

	public static bool BossLevelMessage(BossLevelMessage _message)
	{
		if (m_LocalServer != null)
		{
			m_LocalServer.BroadcastMessageToAll(MessageType.BossLevel, _message);
			return true;
		}
		return false;
	}

	public static bool TimeSync(float fTime)
	{
		if (m_LocalServer != null)
		{
			m_TimeSyncMessage.Initialise(fTime);
			m_LocalServer.BroadcastMessageToAll(MessageType.TimeSync, m_TimeSyncMessage, false);
			return true;
		}
		return false;
	}

	public static bool GameSetup(GameMode mode)
	{
		if (m_LocalServer != null)
		{
			m_GameSetupMessage.Initialise(mode);
			m_LocalServer.BroadcastMessageToAll(MessageType.GameSetup, m_GameSetupMessage);
			return true;
		}
		return false;
	}

	public static bool GameProgressData(GameProgress.GameProgressData progressData, bool[] metaDialogShownStatus)
	{
		if (m_LocalServer != null)
		{
			m_GameProgressDataMessage.ProgressData = progressData;
			m_GameProgressDataMessage.MetaDialogsShownStatus = metaDialogShownStatus;
			m_LocalServer.BroadcastMessageToAll(MessageType.GameProgressData, m_GameProgressDataMessage);
			return true;
		}
		return false;
	}

	public static bool SetupCoopSession(int dlcID, GameProgress.GameProgressData progressData, bool[] _metaDialogShownStatus, SessionConfig sessionConfig)
	{
		if (m_LocalServer != null)
		{
			m_SetupCoopSessionDataMessage.m_sessionConfig.m_config = sessionConfig;
			m_SetupCoopSessionDataMessage.m_Progress.ProgressData = progressData;
			m_SetupCoopSessionDataMessage.m_Progress.MetaDialogsShownStatus = _metaDialogShownStatus;
			m_SetupCoopSessionDataMessage.m_DLCID = dlcID;
			m_LocalServer.BroadcastMessageToAll(MessageType.SetupCoopSession, m_SetupCoopSessionDataMessage);
			return true;
		}
		return false;
	}

	public static bool Achievement(GameObject gameObject, int statId, int increment = 1)
	{
		if (m_LocalServer != null)
		{
			EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(gameObject);
			if (entry != null)
			{
				m_AchievementMessage.Initialise(entry.m_Header, statId, increment);
				m_LocalServer.BroadcastMessageToAll(MessageType.Achievement, m_AchievementMessage);
			}
			return true;
		}
		return false;
	}

	public static void SendChefEffectMessage(GameObject _targetChef, ChefEffectMessage.EffectType _effectType, Vector3 _relativeEffectPosition)
	{
		EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(_targetChef);
		SendChefEffectMessage(entry.m_Header.m_uEntityID, _effectType, _relativeEffectPosition);
	}

	public static void SendChefEffectMessage(uint _targetChef, ChefEffectMessage.EffectType _effectType, Vector3 _relativeEffetPosition)
	{
		m_ChefEffectMessage.Initalise(_targetChef, _effectType, _relativeEffetPosition);
		m_LocalServer.BroadcastMessageToAll(MessageType.ChefEffect, m_ChefEffectMessage);
	}

	public static bool TriggerAudioMessage(GameOneShotAudioTag _audioTag, int _layer)
	{
		if (m_LocalServer != null)
		{
			m_triggerAudioMessage.Initialise(_audioTag, _layer);
			m_LocalServer.BroadcastMessageToAll(MessageType.TriggerAudio, m_triggerAudioMessage);
			return true;
		}
		return false;
	}

	public static void HighScores(Serialisable message, IOnlineMultiplayerSessionUserId destination)
	{
		m_LocalServer.SendMessageToClient(destination, MessageType.HighScores, message);
	}

	public static bool SendHostModeConfigChanged(SessionConfig config)
	{
		if (m_LocalServer != null)
		{
			m_hostModeConfigChangedMessage.Initialise(config);
			m_LocalServer.BroadcastMessageToAll(MessageType.SessionConfigSync, m_hostModeConfigChangedMessage);
			return true;
		}
		return false;
	}

	public static void OnServerStarted(Server server)
	{
		m_LocalServer = server;
	}

	public static void OnServerStopped()
	{
		m_LocalServer = null;
	}
}
