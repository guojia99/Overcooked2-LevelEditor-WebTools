using System.Collections.Generic;
using BitStream;
using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class NetworkUtils
{
	public static class CompressFloat
	{
		public static uint Pack(float val, float maxSize, int bits)
		{
			if (val > maxSize)
			{
				val = maxSize;
			}
			else if (val < 0f - maxSize)
			{
				val = 0f - maxSize;
			}
			return (uint)((val + maxSize) * (float)(1 << bits - 1) / (maxSize * 2f));
		}

		public static float UnPack(uint val, float maxSize, int bits)
		{
			if (bits < 32)
			{
				val &= (uint)((1 << bits) - 1);
			}
			return (float)val * maxSize * 2f / (float)(1 << bits - 1) - maxSize;
		}
	}

	public static GenericVoid OnGameProgressLoadedFromNetwork = delegate
	{
	};

	public static Transform FindVisualRoot(GameObject _object)
	{
		MeshLerper meshLerper = _object.RequestComponentRecursive<MeshLerper>();
		if (meshLerper != null)
		{
			return meshLerper.transform;
		}
		return _object.transform;
	}

	public static void RegisterSpawnablePrefab(GameObject _gameObject, GameObject _prefab)
	{
		RegisterSpawnablePrefab(_gameObject, _prefab, null);
	}

	public static void RegisterSpawnablePrefab(GameObject _gameObject, GameObject _prefab, VoidGeneric<GameObject> _callback)
	{
		SpawnableEntityCollection spawnableEntityCollection = _gameObject.RequestComponent<SpawnableEntityCollection>();
		if (spawnableEntityCollection == null)
		{
			spawnableEntityCollection = _gameObject.AddComponent<SpawnableEntityCollection>();
		}
		spawnableEntityCollection.RegisterSpawnable(_prefab, _callback);
	}

	public static GameObject ServerSpawnPrefab(GameObject _gameObject, GameObject _prefab)
	{
		return ServerSpawnPrefab(_gameObject, _prefab, _gameObject.transform.position, _gameObject.transform.rotation);
	}

	public static GameObject ServerSpawnPrefab(GameObject _gameObject, GameObject _prefab, Vector3 _position, Quaternion _rotation)
	{
		INetworkEntitySpawner networkEntitySpawner = _gameObject.RequireInterface<INetworkEntitySpawner>();
		int spawnableID = networkEntitySpawner.GetSpawnableID(_prefab);
		List<VoidGeneric<GameObject>> callbacks = new List<VoidGeneric<GameObject>>();
		GameObject gameObject = networkEntitySpawner.SpawnEntity(spawnableID, _position, _rotation, ref callbacks);
		EntitySerialisationRegistry.ServerRegisterObject(gameObject);
		ComponentCacheRegistry.UpdateObject(gameObject);
		PhysicalAttachment component = gameObject.GetComponent<PhysicalAttachment>();
		if (null != component)
		{
			EntitySerialisationRegistry.ServerRegisterObject(component.m_container.gameObject);
			ComponentCacheRegistry.UpdateObject(component.m_container.gameObject);
			EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(component.m_container.gameObject);
			EntitySerialisationEntry entry2 = EntitySerialisationRegistry.GetEntry(gameObject);
			if (entry2 != null && entry != null)
			{
				ServerMessenger.SpawnPhysicalAttachment(networkEntitySpawner, spawnableID, entry2.m_Header, _position, _rotation, entry.m_Header);
			}
		}
		else
		{
			EntitySerialisationEntry entry3 = EntitySerialisationRegistry.GetEntry(gameObject);
			if (entry3 != null)
			{
				ServerMessenger.SpawnEntity(networkEntitySpawner, spawnableID, entry3.m_Header, _position, _rotation);
			}
		}
		if (callbacks != null && callbacks.Count > 0)
		{
			for (int i = 0; i < callbacks.Count; i++)
			{
				if (callbacks[i] != null)
				{
					callbacks[i](gameObject);
				}
			}
		}
		return gameObject;
	}

	public static void DestroyObject(GameObject _gameObject)
	{
		_gameObject.SetActive(false);
		ServerMessenger.DestroyEntity(_gameObject);
	}

	public static void DestroyObjectsRecursive(GameObject root)
	{
		root.SetActive(false);
		ServerMessenger.DestroyEntities(root);
	}

	public static void OnResumeEntitySyncMessageReceived(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
	{
		ResumeEntitySyncMessage resumeEntitySyncMessage = (ResumeEntitySyncMessage)message;
		EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(resumeEntitySyncMessage.m_header.m_uEntityID);
		ServerWorldObjectSynchroniser serverWorldObjectSynchroniser = entry.m_GameObject.RequireComponent<ServerWorldObjectSynchroniser>();
		serverWorldObjectSynchroniser.ResumeClient(sessionUserId);
	}

	public static void LevelLoadByIndex(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
	{
		ClientGameSetup.PrevScene = SceneManager.GetActiveScene().name;
		MultiplayerController multiplayerController = GameUtils.RequireManager<MultiplayerController>();
		if (MultiplayerController.IsSynchronisationActive())
		{
			multiplayerController.StopSynchronisation();
		}
		multiplayerController.SetLatencyMeasurePaused(true);
		LevelLoadByIndexMessage levelLoadByIndexMessage = (LevelLoadByIndexMessage)message;
		GameSession gameSession = GameUtils.GetGameSession();
		SceneDirectoryData sceneDirectory = gameSession.Progress.GetSceneDirectory();
		SceneDirectoryData.SceneDirectoryEntry sceneDirectoryEntry = sceneDirectory.Scenes[levelLoadByIndexMessage.LevelIndex];
		SceneDirectoryData.PerPlayerCountDirectoryEntry sceneVarient = sceneDirectoryEntry.GetSceneVarient((int)levelLoadByIndexMessage.Players);
		if (sceneVarient != null)
		{
			string sceneName = sceneVarient.SceneName;
			if (sceneName != string.Empty)
			{
				GameSession.GameLevelSettings gameLevelSettings = new GameSession.GameLevelSettings();
				gameLevelSettings.SceneDirectoryVarientEntry = sceneVarient;
				gameSession.LevelSettings = gameLevelSettings;
				gameSession.Progress.SetLastLevelEntered((int)levelLoadByIndexMessage.LevelIndex);
				GameUtils.GetMetaGameProgress().SetLastPlayedTheme(sceneDirectoryEntry.Theme);
				if (levelLoadByIndexMessage.UseLoadingScreen)
				{
					LoadingScreenFlow.LoadScene(sceneName, levelLoadByIndexMessage.m_HideLoadingScreenGameState);
				}
				else
				{
					GameUtils.LoadScene(sceneName);
				}
			}
		}
		else
		{
			Vector2 vector = new Vector2(0.5f * (float)Camera.main.pixelWidth, 0.5f * (float)Camera.main.pixelHeight);
		}
		ClientMessenger.GameState(levelLoadByIndexMessage.m_StartLoadGameState);
	}

	public static void LevelLoadByName(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
	{
		ClientGameSetup.PrevScene = SceneManager.GetActiveScene().name;
		MultiplayerController multiplayerController = GameUtils.RequireManager<MultiplayerController>();
		if (MultiplayerController.IsSynchronisationActive())
		{
			multiplayerController.StopSynchronisation();
		}
		multiplayerController.SetLatencyMeasurePaused(true);
		LevelLoadByNameMessage levelLoadByNameMessage = (LevelLoadByNameMessage)message;
		if (levelLoadByNameMessage.UseLoadingScreen)
		{
			LoadingScreenFlow.LoadScene(levelLoadByNameMessage.m_Scene, levelLoadByNameMessage.m_HideLoadingScreenGameState);
		}
		else
		{
			GameUtils.LoadScene(levelLoadByNameMessage.m_Scene);
		}
		ClientMessenger.GameState(levelLoadByNameMessage.m_StartLoadGameState);
	}

	public static void LoadGameProgressData(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
	{
		GameSession gameSession = GameUtils.GetGameSession();
		GameProgressDataNetworkMessage gameProgressDataNetworkMessage = (GameProgressDataNetworkMessage)message;
		gameSession.Progress.LoadFromNetwork(gameProgressDataNetworkMessage.ProgressData);
		gameSession.m_shownMetaDialogs = gameProgressDataNetworkMessage.MetaDialogsShownStatus;
		if (OnGameProgressLoadedFromNetwork != null)
		{
			OnGameProgressLoadedFromNetwork();
		}
	}

	public static void SetupCoopSession(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
	{
		SetupCoopSessionNetworkMessage setupCoopSessionNetworkMessage = (SetupCoopSessionNetworkMessage)message;
		GameSession gameSession = GameUtils.GetGameSession();
		if ((gameSession == null || gameSession.TypeSettings.Type != GameSession.GameType.Cooperative || gameSession.DLC != setupCoopSessionNetworkMessage.m_DLCID || gameSession.TypeSettings.WorldMapScene == "Lobbies") && T17FrontendFlow.Instance != null)
		{
			T17FrontendFlow.Instance.StartEmptySession(GameSession.GameType.Cooperative, setupCoopSessionNetworkMessage.m_DLCID);
		}
		LoadGameProgressData(sessionUserId, setupCoopSessionNetworkMessage.m_Progress);
		GameProgress.GameProgressData progressData = setupCoopSessionNetworkMessage.m_Progress.ProgressData;
		GameProgress.HighScores highScores = new GameProgress.HighScores();
		for (int i = 0; i < progressData.Levels.Length; i++)
		{
			highScores.Scores.Add(new GameProgress.HighScores.Score
			{
				iLevelID = progressData.Levels[i].LevelId,
				iHighScore = progressData.Levels[i].HighScore,
				iSurvivalModeTime = progressData.Levels[i].SurvivalModeTime
			});
		}
		if (sessionUserId != null)
		{
			User user = UserSystemUtils.FindUser(ClientUserSystem.m_Users, sessionUserId);
			if (user != null)
			{
				gameSession.HighScoreRepository.SetScoresForMachine(user.Machine, setupCoopSessionNetworkMessage.m_DLCID, highScores);
			}
		}
		gameSession.GameModeSessionConfig = setupCoopSessionNetworkMessage.m_sessionConfig.m_config;
	}

	public static void RepeatHighScores(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
	{
		IOnlinePlatformManager onlinePlatformManager = GameUtils.RequireManagerInterface<IOnlinePlatformManager>();
		if (sessionUserId != null)
		{
			ServerMessenger.HighScores(message, null);
		}
		IOnlineMultiplayerSessionCoordinator onlineMultiplayerSessionCoordinator = onlinePlatformManager.OnlineMultiplayerSessionCoordinator();
		IOnlineMultiplayerSessionUserId[] array = onlineMultiplayerSessionCoordinator.Members();
		for (int i = 0; i < array.Length; i++)
		{
			if (array[i].UniqueId != sessionUserId.UniqueId)
			{
				ServerMessenger.HighScores(message, array[i]);
			}
		}
	}

	public static string GetNetworkMessageDescription(Message message)
	{
		string text = "Type: " + message.Type;
		if (message.Type == MessageType.EntitySynchronisation)
		{
			EntitySynchronisationMessage entitySynchronisationMessage = message.Payload as EntitySynchronisationMessage;
			if (entitySynchronisationMessage != null && entitySynchronisationMessage.m_Header != null)
			{
				text = text + ", Entity ID: " + entitySynchronisationMessage.m_Header.m_uEntityID;
				EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(entitySynchronisationMessage.m_Header.m_uEntityID);
				if (entry != null)
				{
					text += ", Entry found ";
					if (null != entry.m_GameObject)
					{
						string text2 = text;
						text = text2 + ", Name: " + entry.m_GameObject.name + ", ServerComponents: " + entry.m_ServerSynchronisedComponents.Count + ", ClientComponents: " + entry.m_ClientSynchronisedComponents.Count;
					}
				}
				else
				{
					text += ", Entry not found";
				}
			}
		}
		if (message.Type == MessageType.EntityEvent)
		{
			EntityEventMessage entityEventMessage = message.Payload as EntityEventMessage;
			if (entityEventMessage != null && entityEventMessage.m_Header != null)
			{
				string text2 = text;
				text = text2 + ", Entity ID: " + entityEventMessage.m_Header.m_uEntityID + ", ComponentID: " + entityEventMessage.m_ComponentId;
				EntitySerialisationEntry entry2 = EntitySerialisationRegistry.GetEntry(entityEventMessage.m_Header.m_uEntityID);
				if (entry2 != null)
				{
					text += ", Entry found ";
					if (null != entry2.m_GameObject)
					{
						text2 = text;
						text = text2 + ", Name: " + entry2.m_GameObject.name + ", ServerComponents: " + entry2.m_ServerSynchronisedComponents.Count + ", ClientComponents: " + entry2.m_ClientSynchronisedComponents.Count;
					}
				}
				else
				{
					text += ", Entry not found";
				}
			}
		}
		return text;
	}

	public static void SelectRandomAvatar()
	{
		AvatarDirectoryData avatarDirectoryData = GameUtils.GetAvatarDirectoryData();
		MetaGameProgress metaGameProgress = GameUtils.GetMetaGameProgress();
		if (!(null != metaGameProgress))
		{
			return;
		}
		ChefAvatarData[] array = null;
		for (int i = 0; i < ClientUserSystem.m_Users.Count; i++)
		{
			User user = ClientUserSystem.m_Users._items[i];
			if (!user.IsLocal)
			{
				continue;
			}
			uint selectedChefAvatar = user.SelectedChefAvatar;
			if (selectedChefAvatar == 127)
			{
				if (array == null)
				{
					array = metaGameProgress.GetUnlockedAvatars();
				}
				int num = Random.Range(0, array.Length);
				ChefAvatarData avatarData = array[num];
				selectedChefAvatar = (uint)avatarDirectoryData.Avatars.FindIndex_Predicate((ChefAvatarData x) => x == avatarData);
				ClientMessenger.ChefAvatar(selectedChefAvatar, user);
			}
		}
	}

	public static User.PartyPersistance GetRemoteUserPartyPersistanceForJoinState(NetConnectionState state)
	{
		IOnlinePlatformManager onlinePlatformManager = GameUtils.RequireManagerInterface<IOnlinePlatformManager>();
		IOnlineMultiplayerConnectionModeCoordinator onlineMultiplayerConnectionModeCoordinator = onlinePlatformManager.OnlineMultiplayerConnectionModeCoordinator();
		if (onlineMultiplayerConnectionModeCoordinator != null)
		{
			if (onlineMultiplayerConnectionModeCoordinator.Mode() == OnlineMultiplayerConnectionMode.eInternet && state == NetConnectionState.Matchmake)
			{
				return User.PartyPersistance.Kick;
			}
			return User.PartyPersistance.Remain;
		}
		return (state == NetConnectionState.AcceptInvite) ? User.PartyPersistance.Remain : User.PartyPersistance.Kick;
	}

	public static bool DeserialiseGameObject(out GameObject gameObject, BitStreamReader reader)
	{
		uint num = reader.ReadUInt32(10);
		gameObject = null;
		if (num != 0)
		{
			EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(num);
			if (entry != null && entry.m_GameObject != null)
			{
				gameObject = entry.m_GameObject;
				return true;
			}
			return false;
		}
		return false;
	}
}
