using System;
using Team17.Online;
using UnityEngine;

public class FrontendPlayWithFriendsMenu : FrontendMenuBehaviour
{
	[SerializeField]
	private FrontendRootMenu m_rootMenu;

	[SerializeField]
	private FrontendSwitchSearch m_switchSearchMenu;

	[SerializeField]
	private FrontendSwitchFriends m_switchFriendsMenu;

	private FrontendPlayerLobby m_playerLobby;

	private KitchenSwitchConnectionModeTask m_switchConnectionModeTask;

	private KitchenSwitchConnectionModeTask.SubMode m_wirelessSubmode;

	private KitchenSwitchConnectionModeTask.SubMode m_internetSubmode;

	protected override void Start()
	{
		base.Start();
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		m_switchConnectionModeTask.CleanUp();
		m_switchConnectionModeTask.onComplete -= OnSwitchConnectionModeComplete;
		KitchenSwitchConnectionModeTask switchConnectionModeTask = m_switchConnectionModeTask;
		switchConnectionModeTask.onResults = (KitchenSwitchConnectionModeTask.OnResults)Delegate.Remove(switchConnectionModeTask.onResults, new KitchenSwitchConnectionModeTask.OnResults(OnSearchResults));
	}

	protected override void SingleTimeInitialize()
	{
		base.SingleTimeInitialize();
		m_switchConnectionModeTask = new KitchenSwitchConnectionModeTask();
		m_switchConnectionModeTask.onComplete += OnSwitchConnectionModeComplete;
		KitchenSwitchConnectionModeTask switchConnectionModeTask = m_switchConnectionModeTask;
		switchConnectionModeTask.onResults = (KitchenSwitchConnectionModeTask.OnResults)Delegate.Combine(switchConnectionModeTask.onResults, new KitchenSwitchConnectionModeTask.OnResults(OnSearchResults));
	}

	public bool IsBusy()
	{
		return m_switchConnectionModeTask != null && m_switchConnectionModeTask.isRunning;
	}

	public override bool Show(GamepadUser currentGamer, BaseMenuBehaviour parent, GameObject invoker, bool hideInvoker = true)
	{
		if (!base.Show(currentGamer, parent, invoker, hideInvoker))
		{
			return false;
		}
		if (T17FrontendFlow.Instance != null)
		{
			T17FrontendFlow.Instance.BlockFocusKitchen = true;
			if (m_playerLobby == null)
			{
				m_playerLobby = T17FrontendFlow.Instance.m_PlayerLobby;
			}
		}
		return true;
	}

	public override bool Hide(bool restoreInvokerState = true, bool isTabSwitch = false)
	{
		if (T17FrontendFlow.Instance != null)
		{
			T17FrontendFlow.Instance.BlockFocusKitchen = false;
		}
		return base.Hide(restoreInvokerState, isTabSwitch);
	}

	public override void Close()
	{
		base.Close();
		if (T17FrontendFlow.Instance != null)
		{
			T17FrontendFlow.Instance.FocusOnMainMenu();
		}
	}

	protected override void Update()
	{
		base.Update();
		if (m_switchConnectionModeTask.isRunning)
		{
			m_switchConnectionModeTask.Update();
		}
	}

	public void OnHostFriendsPressed()
	{
		m_internetSubmode = KitchenSwitchConnectionModeTask.SubMode.Host;
		SwitchConnectionMode(KitchenSwitchConnectionModeTask.Mode.Internet);
	}

	public void OnJoinFriendsPressed()
	{
		m_switchConnectionModeTask.onComplete += OpenFriendsAfterConnectionModeComplete;
		m_internetSubmode = KitchenSwitchConnectionModeTask.SubMode.Search;
		SwitchConnectionMode(KitchenSwitchConnectionModeTask.Mode.Internet);
	}

	public void OnHostWirelessPressed()
	{
		m_wirelessSubmode = KitchenSwitchConnectionModeTask.SubMode.Host;
		SwitchConnectionMode(KitchenSwitchConnectionModeTask.Mode.Wireless);
	}

	public void OnJoinWirelessPressed()
	{
		m_wirelessSubmode = KitchenSwitchConnectionModeTask.SubMode.Search;
		SwitchConnectionMode(KitchenSwitchConnectionModeTask.Mode.Wireless);
	}

	private void SwitchConnectionMode(KitchenSwitchConnectionModeTask.Mode mode)
	{
		if (!m_switchConnectionModeTask.isRunning)
		{
			m_switchConnectionModeTask.connectionMode = mode;
			m_switchConnectionModeTask.adhocSubmode = m_wirelessSubmode;
			m_switchConnectionModeTask.internetSubmode = m_internetSubmode;
			m_switchConnectionModeTask.Start();
		}
	}

	private void OnSwitchConnectionModeComplete(KitchenTaskResult result)
	{
		Close();
		switch (result)
		{
		case KitchenTaskResult.Success:
			if (m_switchConnectionModeTask.connectionMode == KitchenSwitchConnectionModeTask.Mode.Wireless && !m_switchConnectionModeTask.m_hosting && m_rootMenu != null && m_switchSearchMenu != null)
			{
				m_rootMenu.OpenFrontendMenu(m_switchSearchMenu);
			}
			if (m_playerLobby != null)
			{
				m_playerLobby.UpdateCurrentConnectionMode();
			}
			break;
		case KitchenTaskResult.Failure:
		{
			IConnectionModeSwitchStatus status = ConnectionModeSwitcher.GetStatus();
			if (!status.DisplayPlatformDialog())
			{
				T17DialogBox dialog = T17DialogBoxManager.GetDialog(false);
				if (dialog != null)
				{
					string text = status.GetLocalisedResultDescription();
					if (string.IsNullOrEmpty(text))
					{
						text = Localization.Get("Online.ConnectionMode.ConnectionMode.Result.eGeneric");
					}
					dialog.Initialize("Text.Warning", text, "Text.Button.Continue", null, null, T17DialogBox.Symbols.Warning, true, false);
					dialog.Show();
				}
			}
			IPlayerManager playerManager = GameUtils.RequireManagerInterface<IPlayerManager>();
			OfflineOptions offlineOptions = new OfflineOptions
			{
				hostUser = playerManager.GetUser(EngagementSlot.One),
				eAdditionalAction = OfflineOptions.AdditionalAction.None,
				connectionMode = OnlineMultiplayerConnectionMode.eNone
			};
			ConnectionModeSwitcher.RequestConnectionState(NetConnectionState.Offline, offlineOptions);
			break;
		}
		case KitchenTaskResult.Cancelled:
			Debug.Log("Connection mode change cancelled");
			break;
		}
	}

	private void OpenFriendsAfterConnectionModeComplete(KitchenTaskResult result)
	{
		if (result == KitchenTaskResult.Success && m_switchFriendsMenu != null && m_rootMenu != null)
		{
			m_switchFriendsMenu.Hide();
			m_rootMenu.OpenFrontendMenu(m_switchFriendsMenu);
		}
		m_switchConnectionModeTask.onComplete -= OpenFriendsAfterConnectionModeComplete;
	}

	public void OnSearchResults(SearchTask.SearchResultData results)
	{
		if (m_switchSearchMenu != null)
		{
			m_switchSearchMenu.SetResults(results);
		}
	}
}
