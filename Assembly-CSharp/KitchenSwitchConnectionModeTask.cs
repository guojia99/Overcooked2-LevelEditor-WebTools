using System;
using System.Collections;
using Team17.Online;
using UnityEngine;

public class KitchenSwitchConnectionModeTask : KitchenTask
{
	public enum Mode
	{
		Offline = 0,
		Internet = 1,
		Wireless = 2,
		JoinRoom = 3
	}

	public enum SubMode
	{
		AskUser = 0,
		Host = 1,
		Search = 2
	}

	public delegate void OnResults(SearchTask.SearchResultData results);

	public Mode connectionMode;

	public SubMode internetSubmode = SubMode.Host;

	public SubMode adhocSubmode;

	public JoinEnumeratedRoomOptions joinOptions;

	public string joinProgressText = string.Empty;

	public bool m_hosting = true;

	public OnResults onResults;

	private ServerOptions m_ServerOptions = default(ServerOptions);

	private T17DialogBox m_progressBox;

	private string m_successMessage = string.Empty;

	private IEnumerator m_DelayedSuccessEnumerator;

	private float m_startTime;

	private bool m_joiningEnumeratedRoom;

	private readonly bool c_showModeChangeSuccessMessages;

	private readonly bool c_showProgressSpinnerForAdhocSearch;

	private readonly float c_messageMinDisplayTime = 3f;

	private readonly string c_OfflineProgressMessage = "MainMenu.Kitchen.ChangingMode.Offline";

	private readonly string c_AdhocHostProgressMessage = "MainMenu.Kitchen.ChangingMode.Wireless.Host";

	private readonly string c_AdhocSearchProgressMessage = "MainMenu.Kitchen.ChangingMode.Wireless.Search";

	private readonly string c_OnlineProgressMessage = "MainMenu.Kitchen.ChangingMode.Online";

	private readonly string c_OfflineSuccessMessage = "MainMenu.Kitchen.ModeChanged.Offline";

	private readonly string c_AdhocHostSuccessMessage = "MainMenu.Kitchen.ModeChanged.Wireless.Host";

	private readonly string c_OnlineSuccessMessage = "MainMenu.Kitchen.ModeChanged.Online";

	public override void Start()
	{
		m_successMessage = string.Empty;
		m_DelayedSuccessEnumerator = null;
		switch (connectionMode)
		{
		case Mode.Offline:
			RequestOfflineMode();
			break;
		case Mode.Wireless:
			RequestAdhocMode();
			break;
		case Mode.Internet:
			if (internetSubmode == SubMode.Search)
			{
				RequestOfflineMode(OnlineMultiplayerConnectionMode.eInternet);
			}
			else
			{
				RequestOnlineMode();
			}
			break;
		case Mode.JoinRoom:
			JoinEnumeratedRoom();
			break;
		}
		m_status = KitchenTaskStatus.Running;
	}

	public override void CleanUp()
	{
		base.CleanUp();
		ConnectionModeSwitcher.InvalidateCallback(OnRequestOfflineConnectionStateComplete);
		ConnectionModeSwitcher.InvalidateCallback(OnHostAdhocSessionComplete);
		ConnectionModeSwitcher.InvalidateCallback(OnAdhocSearchComplete);
		ConnectionModeSwitcher.InvalidateCallback(OnRequestOnlineConnectionStateComplete);
	}

	private IEnumerator ShowSuccessMessageThenComplete()
	{
		if (m_progressBox != null)
		{
			while (Time.time - m_startTime < c_messageMinDisplayTime)
			{
				yield return null;
			}
			if (c_showModeChangeSuccessMessages)
			{
				m_progressBox.SetMessage(m_successMessage, true);
				m_startTime = Time.time;
				while (Time.time - m_startTime < c_messageMinDisplayTime)
				{
					yield return null;
				}
			}
		}
		TaskComplete(KitchenTaskResult.Success);
	}

	public override void Update()
	{
		base.Update();
		if (m_DelayedSuccessEnumerator != null && !m_DelayedSuccessEnumerator.MoveNext())
		{
			m_DelayedSuccessEnumerator = null;
		}
		if (m_joiningEnumeratedRoom && ConnectionStatus.CurrentConnectionMode() != OnlineMultiplayerConnectionMode.eAdhoc)
		{
			m_joiningEnumeratedRoom = false;
			TaskComplete(KitchenTaskResult.Failure);
		}
	}

	private void RequestOfflineMode()
	{
		OnlineMultiplayerConnectionMode onlineMultiplayerConnectionMode = ((ConnectionStatus.CurrentConnectionMode() == OnlineMultiplayerConnectionMode.eInternet) ? OnlineMultiplayerConnectionMode.eInternet : OnlineMultiplayerConnectionMode.eNone);
		RequestOfflineMode(onlineMultiplayerConnectionMode);
	}

	private void RequestOfflineMode(OnlineMultiplayerConnectionMode connectionMode)
	{
		try
		{
			m_progressBox = T17DialogBoxManager.GetDialog(false);
			if (m_progressBox != null)
			{
				m_progressBox.Initialize("Text.PleaseWait", c_OfflineProgressMessage, null, null, null, T17DialogBox.Symbols.Spinner);
				m_progressBox.Show();
			}
			m_startTime = Time.time;
			m_successMessage = c_OfflineSuccessMessage;
			ConnectionModeSwitcher.RequestConnectionState(NetConnectionState.Offline, new OfflineOptions
			{
				hostUser = m_IPlayerManager.GetUser(EngagementSlot.One),
				connectionMode = connectionMode
			}, OnRequestOfflineConnectionStateComplete);
		}
		catch (UnityException)
		{
			TaskComplete(KitchenTaskResult.Failure);
		}
	}

	private void OnRequestOfflineConnectionStateComplete(IConnectionModeSwitchStatus status)
	{
		if (status.GetResult() == eConnectionModeSwitchResult.Success)
		{
			m_DelayedSuccessEnumerator = ShowSuccessMessageThenComplete();
			return;
		}
		CompositeStatus compositeStatus = status as CompositeStatus;
		ConnectionModeStatus connectionModeStatus = null;
		if (compositeStatus != null)
		{
			connectionModeStatus = compositeStatus.m_TaskSubStatus as ConnectionModeStatus;
		}
		if (connectionModeStatus != null && connectionModeStatus.m_Result.m_returnCode == OnlineMultiplayerConnectionModeConnectResult.eCancelledByUser)
		{
			TaskComplete(KitchenTaskResult.Cancelled);
		}
		else
		{
			TaskComplete(KitchenTaskResult.Failure);
		}
	}

	private void RequestAdhocMode()
	{
		switch (adhocSubmode)
		{
		case SubMode.Host:
			HostAdhocSession();
			break;
		case SubMode.Search:
			SearchForAdhocSession();
			break;
		case SubMode.AskUser:
		{
			T17DialogBox dialog = T17DialogBoxManager.GetDialog(false);
			if (dialog != null)
			{
				dialog.Initialize("MainMenu.Kitchen.AdHoc.HostOrSearch.Title", "MainMenu.Kitchen.AdHoc.HostOrSearch.Body", "Text.Button.Host", "Text.Button.Search", "Text.Button.Cancel");
				dialog.OnConfirm = (T17DialogBox.DialogEvent)Delegate.Combine(dialog.OnConfirm, new T17DialogBox.DialogEvent(HostAdhocSession));
				dialog.OnDecline = (T17DialogBox.DialogEvent)Delegate.Combine(dialog.OnDecline, new T17DialogBox.DialogEvent(SearchForAdhocSession));
				dialog.OnCancel = (T17DialogBox.DialogEvent)Delegate.Combine(dialog.OnCancel, new T17DialogBox.DialogEvent(CancelConnectionModeSwitch));
				dialog.Show();
			}
			break;
		}
		}
	}

	private void HostAdhocSession()
	{
		m_hosting = true;
		m_ServerOptions.gameMode = GameMode.OnlineKitchen;
		m_ServerOptions.visibility = OnlineMultiplayerSessionVisibility.ePrivate;
		m_ServerOptions.hostUser = m_IPlayerManager.GetUser(EngagementSlot.One);
		m_ServerOptions.connectionMode = OnlineMultiplayerConnectionMode.eAdhoc;
		try
		{
			m_progressBox = T17DialogBoxManager.GetDialog(false);
			if (m_progressBox != null)
			{
				m_progressBox.Initialize("Text.PleaseWait", c_AdhocHostProgressMessage, null, null, null, T17DialogBox.Symbols.Spinner);
				m_progressBox.Show();
			}
			m_startTime = Time.time;
			m_successMessage = c_AdhocHostSuccessMessage;
			DisconnectionHandler.ConnectionModeErrorEvent = (GenericVoid<OnlineMultiplayerReturnCode<OnlineMultiplayerConnectionModeErrorResult>>)Delegate.Combine(DisconnectionHandler.ConnectionModeErrorEvent, new GenericVoid<OnlineMultiplayerReturnCode<OnlineMultiplayerConnectionModeErrorResult>>(OnConnectionModeError));
			ConnectionModeSwitcher.RequestConnectionState(NetConnectionState.Server, m_ServerOptions, OnHostAdhocSessionComplete);
		}
		catch (UnityException)
		{
			TaskComplete(KitchenTaskResult.Failure);
		}
	}

	private void OnHostAdhocSessionComplete(IConnectionModeSwitchStatus status)
	{
		DisconnectionHandler.ConnectionModeErrorEvent = (GenericVoid<OnlineMultiplayerReturnCode<OnlineMultiplayerConnectionModeErrorResult>>)Delegate.Remove(DisconnectionHandler.ConnectionModeErrorEvent, new GenericVoid<OnlineMultiplayerReturnCode<OnlineMultiplayerConnectionModeErrorResult>>(OnConnectionModeError));
		if (status.GetResult() == eConnectionModeSwitchResult.Success)
		{
			m_DelayedSuccessEnumerator = ShowSuccessMessageThenComplete();
		}
		else
		{
			TaskComplete(KitchenTaskResult.Failure);
		}
	}

	private void SearchForAdhocSession()
	{
		m_hosting = false;
		if (c_showProgressSpinnerForAdhocSearch)
		{
			m_progressBox = T17DialogBoxManager.GetDialog(false);
			if (m_progressBox != null)
			{
				m_progressBox.Initialize("Text.PleaseWait", c_AdhocSearchProgressMessage, null, null, null, T17DialogBox.Symbols.Spinner);
				m_progressBox.Show();
			}
		}
		GamepadUser user = m_IPlayerManager.GetUser(EngagementSlot.One);
		ConnectionModeSwitcher.RequestConnectionState(NetConnectionState.Offline, new OfflineOptions
		{
			hostUser = user,
			searchGameMode = GameMode.OnlineKitchen,
			eAdditionalAction = OfflineOptions.AdditionalAction.PrivilegeCheckAllUsersAndSearchForGames,
			connectionMode = OnlineMultiplayerConnectionMode.eAdhoc
		}, OnAdhocSearchComplete);
	}

	private void OnAdhocSearchComplete(IConnectionModeSwitchStatus status)
	{
		if (status != null && status.GetResult() == eConnectionModeSwitchResult.Success)
		{
			SearchTask.SearchResultData searchResultData = ConnectionModeSwitcher.GetAgentData() as SearchTask.SearchResultData;
			if (searchResultData != null && searchResultData.m_AvailableSessions != null && onResults != null)
			{
				onResults(searchResultData);
			}
			TaskComplete(KitchenTaskResult.Success);
		}
		else
		{
			TaskComplete(KitchenTaskResult.Failure);
		}
	}

	private void CancelConnectionModeSwitch()
	{
		TaskComplete(KitchenTaskResult.Cancelled);
	}

	private void RequestOnlineMode()
	{
		m_ServerOptions.gameMode = GameMode.OnlineKitchen;
		m_ServerOptions.visibility = OnlineMultiplayerSessionVisibility.ePrivate;
		m_ServerOptions.hostUser = m_IPlayerManager.GetUser(EngagementSlot.One);
		m_ServerOptions.connectionMode = OnlineMultiplayerConnectionMode.eInternet;
		try
		{
			m_progressBox = T17DialogBoxManager.GetDialog(false);
			if (m_progressBox != null)
			{
				m_progressBox.Initialize("Text.PleaseWait", c_OnlineProgressMessage, null, null, null, T17DialogBox.Symbols.Spinner);
				m_progressBox.Show();
			}
			m_startTime = Time.time;
			m_successMessage = c_OnlineSuccessMessage;
			DisconnectionHandler.ConnectionModeErrorEvent = (GenericVoid<OnlineMultiplayerReturnCode<OnlineMultiplayerConnectionModeErrorResult>>)Delegate.Combine(DisconnectionHandler.ConnectionModeErrorEvent, new GenericVoid<OnlineMultiplayerReturnCode<OnlineMultiplayerConnectionModeErrorResult>>(OnConnectionModeError));
			ConnectionModeSwitcher.RequestConnectionState(NetConnectionState.Server, m_ServerOptions, OnRequestOnlineConnectionStateComplete);
		}
		catch (UnityException)
		{
			TaskComplete(KitchenTaskResult.Failure);
		}
	}

	private void OnRequestOnlineConnectionStateComplete(IConnectionModeSwitchStatus status)
	{
		DisconnectionHandler.ConnectionModeErrorEvent = (GenericVoid<OnlineMultiplayerReturnCode<OnlineMultiplayerConnectionModeErrorResult>>)Delegate.Remove(DisconnectionHandler.ConnectionModeErrorEvent, new GenericVoid<OnlineMultiplayerReturnCode<OnlineMultiplayerConnectionModeErrorResult>>(OnConnectionModeError));
		if (status.GetResult() == eConnectionModeSwitchResult.Success)
		{
			m_DelayedSuccessEnumerator = ShowSuccessMessageThenComplete();
			return;
		}
		CompositeStatus compositeStatus = status as CompositeStatus;
		ConnectionModeStatus connectionModeStatus = null;
		if (compositeStatus != null)
		{
			connectionModeStatus = compositeStatus.m_TaskSubStatus as ConnectionModeStatus;
		}
		if (connectionModeStatus != null && connectionModeStatus.m_Result.m_returnCode == OnlineMultiplayerConnectionModeConnectResult.eCancelledByUser)
		{
			TaskComplete(KitchenTaskResult.Cancelled);
		}
		else
		{
			TaskComplete(KitchenTaskResult.Failure);
		}
	}

	private void JoinEnumeratedRoom()
	{
		if (joinOptions == null)
		{
			TaskComplete(KitchenTaskResult.Failure);
			return;
		}
		m_progressBox = T17DialogBoxManager.GetDialog(false);
		if (m_progressBox != null)
		{
			m_progressBox.Initialize("Text.PleaseWait", joinProgressText, null, null, null, T17DialogBox.Symbols.Spinner, true, false);
			m_progressBox.Show();
		}
		m_startTime = Time.time;
		m_successMessage = string.Empty;
		m_joiningEnumeratedRoom = ConnectionModeSwitcher.RequestConnectionState(NetConnectionState.JoinEnumeratedRoom, joinOptions, OnJoinEnumeratedRoomComplete);
	}

	private void OnJoinEnumeratedRoomComplete(IConnectionModeSwitchStatus status)
	{
		joinOptions = null;
		joinProgressText = string.Empty;
		m_joiningEnumeratedRoom = false;
		if (status.GetResult() == eConnectionModeSwitchResult.Success)
		{
			m_DelayedSuccessEnumerator = ShowSuccessMessageThenComplete();
		}
		else
		{
			TaskComplete(KitchenTaskResult.Failure);
		}
	}

	private void OnConnectionModeError(OnlineMultiplayerReturnCode<OnlineMultiplayerConnectionModeErrorResult> result)
	{
		DisconnectionHandler.ConnectionModeErrorEvent = (GenericVoid<OnlineMultiplayerReturnCode<OnlineMultiplayerConnectionModeErrorResult>>)Delegate.Remove(DisconnectionHandler.ConnectionModeErrorEvent, new GenericVoid<OnlineMultiplayerReturnCode<OnlineMultiplayerConnectionModeErrorResult>>(OnConnectionModeError));
		TaskComplete(KitchenTaskResult.Failure);
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
