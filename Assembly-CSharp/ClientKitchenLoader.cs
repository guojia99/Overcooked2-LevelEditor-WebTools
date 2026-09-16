using System;
using System.Collections.Generic;
using Team17.Online;
using Team17.Online.Multiplayer;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientKitchenLoader : MonoBehaviour
{
	private MultiplayerController m_Controller;

	private Client m_Client;

	private PlayerSwitchingManager m_PlayerSwitchingManager;

	private NetworkErrorDialog m_NetworkErrorDialog = new NetworkErrorDialog();

	private GameState m_GameState;

	private const float m_fSynchroniserStartupTimerDuration = 10f;

	private float m_fSynchroniserStartupTimer;

	public static event GenericVoid OnChefsSetupComplete;

	private void Awake()
	{
		Mailbox.Client.RegisterForMessageType(MessageType.GameState, OnGameStateChanged);
		Mailbox.Client.RegisterForMessageType(MessageType.ChefOwnership, AssignChefOwnershipFromNetwork);
		m_NetworkErrorDialog.Enable(OnNetworkDisconnectionConfirmed);
	}

	private void Start()
	{
		m_PlayerSwitchingManager = GameUtils.RequireManager<PlayerSwitchingManager>();
	}

	private void Update()
	{
		switch (m_GameState)
		{
		case GameState.ScanNetworkEntities:
			if (CheckScanned())
			{
				ScannedEntities();
			}
			break;
		case GameState.StartEntities:
			if (CheckStarted())
			{
				StartEntities();
				break;
			}
			m_fSynchroniserStartupTimer += Time.deltaTime;
			if (m_fSynchroniserStartupTimer >= 10f)
			{
				StartEntities();
			}
			break;
		}
	}

	private void ScannedEntities()
	{
		UserSystemUtils.BuildGameInputConfig();
		ClientMessenger.GameState(GameState.ScannedNetworkEntities);
	}

	private bool CheckScanned()
	{
		if (m_Controller.ScanActive || ComponentCacheRegistry.ScanActive)
		{
			return false;
		}
		return true;
	}

	private void StartEntities()
	{
		ClientMessenger.GameState(GameState.StartedEntities);
		m_GameState = GameState.NotSet;
	}

	private bool CheckStarted()
	{
		bool flag = true;
		FastList<EntitySerialisationEntry> entitiesList = EntitySerialisationRegistry.m_EntitiesList;
		int num = 0;
		while (flag && num < entitiesList.Count)
		{
			EntitySerialisationEntry entitySerialisationEntry = entitiesList._items[num];
			int num2 = 0;
			while (flag && num2 < entitySerialisationEntry.m_ClientSynchronisedComponents.Count)
			{
				ClientSynchroniser clientSynchroniser = entitySerialisationEntry.m_ClientSynchronisedComponents._items[num2];
				if (clientSynchroniser.GetEntityType() == EntityType.WorldObject || clientSynchroniser.GetEntityType() == EntityType.Chef)
				{
					ClientWorldObjectSynchroniser clientWorldObjectSynchroniser = clientSynchroniser as ClientWorldObjectSynchroniser;
					if (null != clientWorldObjectSynchroniser && !clientWorldObjectSynchroniser.m_bHasEverReceived)
					{
						flag = false;
						break;
					}
				}
				num2++;
			}
			num++;
		}
		return flag;
	}

	private void OnDestroy()
	{
		Mailbox.Client.UnregisterForMessageType(MessageType.GameState, OnGameStateChanged);
		Mailbox.Client.UnregisterForMessageType(MessageType.ChefOwnership, AssignChefOwnershipFromNetwork);
		m_NetworkErrorDialog.Disable();
	}

	public void Initialise(Client client, MultiplayerController controller)
	{
		m_Client = client;
		m_Controller = controller;
		ClientMessenger.GameState(GameState.LoadedKitchen);
	}

	private void OnGameStateChanged(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
	{
		GameStateMessage gameStateMessage = (GameStateMessage)message;
		m_GameState = gameStateMessage.m_State;
		switch (gameStateMessage.m_State)
		{
		case GameState.ScanNetworkEntities:
			m_Controller.ScanEntities(delegate
			{
				ComponentCacheRegistry.ScanForInitialObjects();
			});
			if (CheckScanned())
			{
				ScannedEntities();
			}
			break;
		case GameState.StartSynchronising:
			m_Controller.StartSynchronisation();
			m_fSynchroniserStartupTimer = 0f;
			ClientMessenger.GameState(GameState.StartedSyncronising);
			break;
		case GameState.StartEntities:
			if (CheckStarted())
			{
				StartEntities();
			}
			break;
		case GameState.RunLevelIntro:
		{
			MultiplayerController multiplayerController = GameUtils.RequireManager<MultiplayerController>();
			multiplayerController.SetLatencyMeasurePaused(false);
			if (!LoadingScreenFlow.IsLoadingStartScreen())
			{
				InviteMonitor.SwitchHandlerType(InviteMonitor.HandlerType.Gameplay);
			}
			break;
		}
		}
	}

	private void OnNetworkDisconnectionConfirmed()
	{
		ServerGameSetup.Mode = GameMode.OnlineKitchen;
		LoadingScreenFlow.LoadScene("StartScreen");
	}

	private void AssignChefOwnershipFromNetwork(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
	{
		m_Client.GetUserSystem().OnUsersChanged(sessionUserId, message);
		int num = 0;
		for (int i = 0; i < ClientUserSystem.m_Users.Count; i++)
		{
			User user = ClientUserSystem.m_Users._items[i];
			if (user.IsLocal)
			{
				PlayerInputLookup.Player currentPlayer = (PlayerInputLookup.Player)num;
				SetupMyChef(user.EntityID, currentPlayer);
				SetupMyChef(user.Entity2ID, currentPlayer);
				num++;
			}
			else
			{
				SetupOtherChef(user.EntityID, user.Machine, PlayerInputLookup.Player.Count);
				SetupOtherChef(user.Entity2ID, user.Machine, PlayerInputLookup.Player.Count);
			}
		}
		if (ClientKitchenLoader.OnChefsSetupComplete != null)
		{
			ClientKitchenLoader.OnChefsSetupComplete();
		}
		m_PlayerSwitchingManager.InitialiseAvatars();
		FastList<GameObject> fastList = new FastList<GameObject>();
		int count = ClientUserSystem.m_Users.Count;
		for (int j = 0; j < count; j++)
		{
			User user2 = ClientUserSystem.m_Users._items[j];
			GameObject gameObject = ReplaceMesh(user2.EntityID, user2.SelectedChefData);
			if (null != gameObject)
			{
				fastList.Add(gameObject);
			}
			gameObject = ReplaceMesh(user2.Entity2ID, user2.SelectedChefData);
			if (null != gameObject)
			{
				fastList.Add(gameObject);
			}
		}
		FastList<GameObject> fastList2 = new FastList<GameObject>();
		count = PlayerIDProvider.s_AllProviders.Count;
		for (int k = 0; k < count; k++)
		{
			GameObject item = PlayerIDProvider.s_AllProviders._items[k].gameObject;
			if (!fastList.Contains(item))
			{
				fastList2.Add(item);
			}
		}
		count = fastList2.Count;
		for (int l = 0; l < count; l++)
		{
			GameObject obj = fastList2._items[l];
			EntitySerialisationRegistry.UnregisterObject(obj);
			UnityEngine.Object.Destroy(obj);
		}
		fastList2.Clear();
		count = PlayerIDProvider.s_AllProviders.Count;
		for (int m = 0; m < count; m++)
		{
			PlayerIDProvider playerIDProvider = PlayerIDProvider.s_AllProviders._items[m];
			ClientInputTransmitter clientInputTransmitter = playerIDProvider.gameObject.RequireComponent<ClientInputTransmitter>();
			clientInputTransmitter.Setup();
		}
		ClientMessenger.GameState(GameState.AssignedChefsToUsers);
	}

	private GameObject ReplaceMesh(uint uEntityID, GameSession.SelectedChefData selectedChef)
	{
		if (selectedChef != null)
		{
			EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(uEntityID);
			if (entry != null && null != entry.m_GameObject)
			{
				ChefMeshReplacer component = entry.m_GameObject.GetComponent<ChefMeshReplacer>();
				if (null != component)
				{
					component.SetChefData(selectedChef, true);
					return component.gameObject;
				}
			}
		}
		return null;
	}

	private static void SetupMyChef(uint entityID, PlayerInputLookup.Player currentPlayer)
	{
		if (entityID == 0)
		{
			return;
		}
		EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(entityID);
		if (entry != null)
		{
			GameInputConfig baseInputConfig = PlayerInputLookup.GetBaseInputConfig();
			PlayerIDProvider component = entry.m_GameObject.GetComponent<PlayerIDProvider>();
			GameInputConfig.ConfigEntry configEntry = Array.Find(baseInputConfig.m_playerConfigs, (GameInputConfig.ConfigEntry x) => x.Player == currentPlayer);
			if (configEntry != null)
			{
				configEntry.MachineId = ClientUserSystem.s_LocalMachineId;
			}
			component.OverridePlayerId(currentPlayer);
		}
	}

	private static void SetupOtherChef(uint entityID, User.MachineID machineID, PlayerInputLookup.Player currentPlayer)
	{
		if (entityID == 0)
		{
			return;
		}
		EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(entityID);
		if (entry != null)
		{
			GameInputConfig baseInputConfig = PlayerInputLookup.GetBaseInputConfig();
			PlayerIDProvider component = entry.m_GameObject.GetComponent<PlayerIDProvider>();
			GameInputConfig.ConfigEntry configEntry = Array.Find(baseInputConfig.m_playerConfigs, (GameInputConfig.ConfigEntry x) => x.Player == currentPlayer);
			if (configEntry != null)
			{
				configEntry.MachineId = machineID;
			}
			component.OverridePlayerId(PlayerInputLookup.Player.Count);
		}
	}
}
