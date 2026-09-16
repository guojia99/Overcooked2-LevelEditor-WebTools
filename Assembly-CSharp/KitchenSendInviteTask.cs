using Team17.Online;
using UnityEngine;

public class KitchenSendInviteTask : KitchenTask
{
	private ServerOptions m_ServerOptions = default(ServerOptions);

	private T17DialogBox m_progressBox;

	private bool m_RequestOnlineMode;

	public override void Start()
	{
		base.Start();
		m_RequestOnlineMode = false;
		if (!ConnectionStatus.IsInSession())
		{
			if (!CheckForInvalidLocalPlayers())
			{
				m_RequestOnlineMode = true;
			}
			m_status = KitchenTaskStatus.Running;
		}
		else
		{
			ShowInviteUI();
		}
	}

	public override void CleanUp()
	{
		base.CleanUp();
		ConnectionModeSwitcher.InvalidateCallback(OnConnectionStateRequestComplete);
	}

	public override void Update()
	{
		base.Update();
		if (m_RequestOnlineMode)
		{
			RequestOnlineMode();
		}
		if (m_progressBox != null)
		{
			string localisedProgressDescription = ConnectionModeSwitcher.GetStatus().GetLocalisedProgressDescription();
			m_progressBox.SetMessage(localisedProgressDescription, false);
		}
	}

	private bool CheckForInvalidLocalPlayers()
	{
		if (UserSystemUtils.AnySplitPadUsers())
		{
			NetworkDialogHelper.ShowRemoveSplitPadUsersDialog(RemoveLocalGuestsBeforeInvite, CancelInvite);
			return true;
		}
		return false;
	}

	private void RemoveLocalGuestsBeforeInvite()
	{
		UserSystemUtils.RemoveAllSplitPadGuestUsers();
		m_RequestOnlineMode = true;
	}

	private void CancelInvite()
	{
		TaskComplete(KitchenTaskResult.Success);
	}

	private void RequestOnlineMode()
	{
		m_RequestOnlineMode = false;
		m_ServerOptions.gameMode = GameMode.OnlineKitchen;
		m_ServerOptions.visibility = OnlineMultiplayerSessionVisibility.ePrivate;
		m_ServerOptions.hostUser = m_IPlayerManager.GetUser(EngagementSlot.One);
		m_ServerOptions.connectionMode = OnlineMultiplayerConnectionMode.eInternet;
		try
		{
			m_progressBox = T17DialogBoxManager.GetDialog(false);
			if (m_progressBox != null)
			{
				m_progressBox.Initialize("Text.PleaseWait", string.Empty, null, null, null, T17DialogBox.Symbols.Spinner);
				m_progressBox.Show();
			}
			ConnectionModeSwitcher.RequestConnectionState(NetConnectionState.Server, m_ServerOptions, OnConnectionStateRequestComplete);
		}
		catch (UnityException)
		{
			TaskComplete(KitchenTaskResult.Failure);
		}
	}

	private void OnConnectionStateRequestComplete(IConnectionModeSwitchStatus status)
	{
		if (status.GetResult() == eConnectionModeSwitchResult.Success)
		{
			ShowInviteUI();
		}
		else
		{
			TaskComplete(KitchenTaskResult.Failure);
		}
	}

	private bool ShowInviteUI()
	{
		bool result = false;
		IOnlinePlatformManager onlinePlatformManager = GameUtils.RequireManagerInterface<IOnlinePlatformManager>();
		IOnlineMultiplayerSessionCoordinator onlineMultiplayerSessionCoordinator = onlinePlatformManager.OnlineMultiplayerSessionCoordinator();
		if (onlineMultiplayerSessionCoordinator != null)
		{
			string msg = Localization.Get("Online.GameInvite.Message");
			onlineMultiplayerSessionCoordinator.ShowSendInviteDialog(msg);
			result = true;
		}
		TaskComplete(KitchenTaskResult.Success);
		return result;
	}

	protected override void TaskComplete(KitchenTaskResult result)
	{
		if (m_progressBox != null)
		{
			m_progressBox.Hide();
			m_progressBox = null;
		}
		base.TaskComplete(result);
	}
}
