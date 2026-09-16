using System.Collections.Generic;
using Team17.Online;
using Team17.Online.Multiplayer;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientMapLoader : MonoBehaviour
{
	private MultiplayerController m_Controller;

	private Client m_Client;

	private GameState m_GameState;

	private const float m_fSynchroniserStartupTimerDuration = 1f;

	private float m_fSynchroniserStartupTimer;

	private void Awake()
	{
		Mailbox.Client.RegisterForMessageType(MessageType.GameState, OnGameStateChanged);
	}

	private void OnDestroy()
	{
		Mailbox.Client.UnregisterForMessageType(MessageType.GameState, OnGameStateChanged);
	}

	private void Update()
	{
		switch (m_GameState)
		{
		case GameState.MapScanNetworkEntities:
			if (CheckScanned())
			{
				ScannedEntities();
			}
			break;
		case GameState.MapStartEntities:
			if (CheckStarted())
			{
				StartEntities();
				break;
			}
			m_fSynchroniserStartupTimer += Time.deltaTime;
			if (m_fSynchroniserStartupTimer >= 1f)
			{
				StartEntities();
			}
			break;
		}
	}

	public void Initialise(Client client, MultiplayerController controller)
	{
		m_Client = client;
		m_Controller = controller;
		ClientMessenger.GameState(GameState.LoadedMap);
	}

	private void ScannedEntities()
	{
		ClientMessenger.GameState(GameState.MapScannedNetworkEntities);
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
		Camera main = Camera.main;
		WorldMapCamera worldMapCamera = main.gameObject.RequireComponent<WorldMapCamera>();
		worldMapCamera.Initialise();
		ClientMessenger.GameState(GameState.MapStartedEntities);
		m_GameState = GameState.MapStartedEntities;
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
				if (clientSynchroniser.GetEntityType() == EntityType.WorldObject || clientSynchroniser.GetEntityType() == EntityType.WorldMapVanAvatar)
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

	private void OnGameStateChanged(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
	{
		GameStateMessage gameStateMessage = (GameStateMessage)message;
		m_GameState = gameStateMessage.m_State;
		switch (gameStateMessage.m_State)
		{
		case GameState.MapScanNetworkEntities:
			m_Controller.ScanEntities();
			if (CheckScanned())
			{
				ScannedEntities();
			}
			break;
		case GameState.MapStartSynchronising:
			m_Controller.StartSynchronisation();
			m_fSynchroniserStartupTimer = 0f;
			ClientMessenger.GameState(GameState.MapStartedSyncronising);
			break;
		case GameState.MapStartEntities:
			if (CheckStarted())
			{
				StartEntities();
			}
			break;
		}
	}
}
