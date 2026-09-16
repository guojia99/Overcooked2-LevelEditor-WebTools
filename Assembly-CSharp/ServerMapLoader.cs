using Team17.Online;
using UnityEngine;

public class ServerMapLoader : MonoBehaviour
{
	private GameState m_State;

	private void Awake()
	{
		m_State = GameState.LoadMap;
	}

	private void Start()
	{
	}

	private void Update()
	{
		switch (m_State)
		{
		case GameState.LoadMap:
			if (AreAllUsersInGameState(GameState.LoadedMap))
			{
				ChangeGameState(GameState.MapScanNetworkEntities);
			}
			break;
		case GameState.MapScanNetworkEntities:
			if (AreAllUsersInGameState(GameState.MapScannedNetworkEntities))
			{
				ChangeGameState(GameState.MapStartSynchronising);
			}
			break;
		case GameState.MapStartSynchronising:
			if (AreAllUsersInGameState(GameState.MapStartedSyncronising))
			{
				ChangeGameState(GameState.MapStartEntities);
			}
			break;
		case GameState.MapStartEntities:
			if (AreAllUsersInGameState(GameState.MapStartedEntities))
			{
				ChangeGameState(GameState.RunMapUnfoldRoutine);
			}
			break;
		case GameState.LoadedMap:
		case GameState.MapScannedNetworkEntities:
		case GameState.MapStartedSyncronising:
			break;
		}
	}

	private void OnDestroy()
	{
	}

	private void ChangeGameState(GameState state)
	{
		UserSystemUtils.ChangeGameState(state);
		m_State = state;
	}

	private bool AreAllUsersInGameState(GameState state)
	{
		return UserSystemUtils.AreAllUsersInGameState(ServerUserSystem.m_Users, state);
	}
}
