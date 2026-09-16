using Team17.Online;
using UnityEngine;

public class ServerKitchenLoader : MonoBehaviour
{
	private GameState m_State;

	private KitchenBootstrapManager m_KitchenBootstap;

	private void Awake()
	{
		m_State = GameState.LoadKitchen;
		m_KitchenBootstap = GameUtils.RequireManager<KitchenBootstrapManager>();
	}

	private void Start()
	{
	}

	private void Update()
	{
		switch (m_State)
		{
		case GameState.LoadKitchen:
			if (AreAllUsersInGameState(GameState.LoadedKitchen))
			{
				ChangeGameState(GameState.ScanNetworkEntities);
			}
			break;
		case GameState.ScanNetworkEntities:
			if (AreAllUsersInGameState(GameState.ScannedNetworkEntities))
			{
				ChangeGameState(GameState.StartSynchronising);
			}
			break;
		case GameState.StartSynchronising:
		{
			if (!AreAllUsersInGameState(GameState.StartedSyncronising))
			{
				break;
			}
			ChangeGameState(GameState.AssignChefsToUsers);
			for (int i = 0; i < ServerUserSystem.m_Users.Count; i++)
			{
				User user = ServerUserSystem.m_Users._items[i];
				if (user.SelectedChefData == null)
				{
					user.SelectedChefData = m_KitchenBootstap.GetDefaultChef(i);
				}
			}
			KitchenLoaderManager kitchenLoaderManager = GameUtils.RequestManagerInterface<KitchenLoaderManager>();
			kitchenLoaderManager.AssignChefEntities(ServerUserSystem.m_Users);
			ServerMessenger.ChefOwnership();
			break;
		}
		case GameState.AssignChefsToUsers:
			if (AreAllUsersInGameState(GameState.AssignedChefsToUsers))
			{
				ChangeGameState(GameState.StartEntities);
			}
			break;
		case GameState.StartEntities:
			if (AreAllUsersInGameState(GameState.StartedEntities))
			{
				ChangeGameState(GameState.RunKitchen);
				IServerFlowController serverFlowController = GameUtils.RequireManagerInterface<IServerFlowController>();
				if (serverFlowController != null)
				{
					serverFlowController.StartFlow();
				}
			}
			break;
		case GameState.LoadedKitchen:
		case GameState.ScannedNetworkEntities:
		case GameState.StartedSyncronising:
		case GameState.AssignedChefsToUsers:
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
