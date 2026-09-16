using System;
using Team17.Online;

public class NetworkErrorDialog
{
	private static T17DialogBox m_dialogBox;

	private static T17DialogBox.DialogEvent OnNetworkErrorDialogDismissed;

	private T17DialogBox.DialogEvent m_LocalConfirmCallbacks;

	public void Enable(T17DialogBox.DialogEvent onNetworkErrorDialogDismissed)
	{
		m_LocalConfirmCallbacks = onNetworkErrorDialogDismissed;
		OnNetworkErrorDialogDismissed = (T17DialogBox.DialogEvent)Delegate.Combine(OnNetworkErrorDialogDismissed, m_LocalConfirmCallbacks);
		DisconnectionHandler.SessionConnectionLostEvent = (GenericVoid)Delegate.Combine(DisconnectionHandler.SessionConnectionLostEvent, new GenericVoid(OnSessionConnectionLost));
		DisconnectionHandler.ConnectionModeErrorEvent = (GenericVoid<OnlineMultiplayerReturnCode<OnlineMultiplayerConnectionModeErrorResult>>)Delegate.Combine(DisconnectionHandler.ConnectionModeErrorEvent, new GenericVoid<OnlineMultiplayerReturnCode<OnlineMultiplayerConnectionModeErrorResult>>(OnConnectionModeError));
		DisconnectionHandler.KickedFromSessionEvent = (GenericVoid)Delegate.Combine(DisconnectionHandler.KickedFromSessionEvent, new GenericVoid(OnKickedFromSession));
		DisconnectionHandler.LocalDisconnectionEvent = (GenericVoid<OnlineMultiplayerReturnCode<OnlineMultiplayerSessionDisconnectionResult>>)Delegate.Combine(DisconnectionHandler.LocalDisconnectionEvent, new GenericVoid<OnlineMultiplayerReturnCode<OnlineMultiplayerSessionDisconnectionResult>>(OnLocalDisconnection));
	}

	public void Disable()
	{
		DisconnectionHandler.SessionConnectionLostEvent = (GenericVoid)Delegate.Remove(DisconnectionHandler.SessionConnectionLostEvent, new GenericVoid(OnSessionConnectionLost));
		DisconnectionHandler.ConnectionModeErrorEvent = (GenericVoid<OnlineMultiplayerReturnCode<OnlineMultiplayerConnectionModeErrorResult>>)Delegate.Remove(DisconnectionHandler.ConnectionModeErrorEvent, new GenericVoid<OnlineMultiplayerReturnCode<OnlineMultiplayerConnectionModeErrorResult>>(OnConnectionModeError));
		DisconnectionHandler.KickedFromSessionEvent = (GenericVoid)Delegate.Remove(DisconnectionHandler.KickedFromSessionEvent, new GenericVoid(OnKickedFromSession));
		DisconnectionHandler.LocalDisconnectionEvent = (GenericVoid<OnlineMultiplayerReturnCode<OnlineMultiplayerSessionDisconnectionResult>>)Delegate.Remove(DisconnectionHandler.LocalDisconnectionEvent, new GenericVoid<OnlineMultiplayerReturnCode<OnlineMultiplayerSessionDisconnectionResult>>(OnLocalDisconnection));
		OnNetworkErrorDialogDismissed = (T17DialogBox.DialogEvent)Delegate.Remove(OnNetworkErrorDialogDismissed, m_LocalConfirmCallbacks);
		m_LocalConfirmCallbacks = null;
	}

	public void OnDestroy()
	{
		Disable();
	}

	private void OnSessionConnectionLost()
	{
		ShowDialog(NetworkErrors.GetDisconnectionMessageText(OnlineMultiplayerSessionDisconnectionResult.eGeneric), true);
	}

	private void OnConnectionModeError(OnlineMultiplayerReturnCode<OnlineMultiplayerConnectionModeErrorResult> result)
	{
		ShowDialog(result);
	}

	private void OnKickedFromSession()
	{
		ShowDialog(NetworkErrors.GetDisconnectionMessageText(OnlineMultiplayerSessionDisconnectionResult.eKicked), true);
	}

	private void OnLocalDisconnection(OnlineMultiplayerReturnCode<OnlineMultiplayerSessionDisconnectionResult> result)
	{
		ShowDialog(result);
	}

	public static void ShowDialog(OnlineMultiplayerReturnCode<OnlineMultiplayerSessionDisconnectionResult> result)
	{
		result.DisplayPlatformSpecificError();
		ShowDialog(NetworkErrors.GetDisconnectionMessageText(result.m_returnCode), true);
	}

	public static void ShowDialog(OnlineMultiplayerReturnCode<OnlineMultiplayerConnectionModeErrorResult> result)
	{
		result.DisplayPlatformSpecificError();
		ShowDialog(NetworkErrors.GetDisconnectionMessageText(result.m_returnCode), true);
	}

	public static void ShowDialog(IConnectionModeSwitchStatus status)
	{
		status.DisplayPlatformDialog();
		ShowDialog(status.GetLocalisedResultDescription(), false);
	}

	public static void ShowDialog(JoinSessionStatus joinStatus, bool bShowPlatformSpecificError)
	{
		if (bShowPlatformSpecificError)
		{
			joinStatus.DisplayPlatformDialog();
		}
		ShowDialog(joinStatus.GetLocalisedResultDescription(), false);
	}

	private static void ShowDialog(string message, bool localiseMessage)
	{
		if (m_dialogBox == null)
		{
			m_dialogBox = T17DialogBoxManager.GetDialog(false);
			if (m_dialogBox != null)
			{
				T17DialogBox dialogBox = m_dialogBox;
				string title = "Text.Warning";
				string confirmBtn = "Text.Button.Confirm";
				string declineBtn = null;
				string cancelBtn = null;
				bool bLocalizeMessage = localiseMessage;
				dialogBox.Initialize(title, message, confirmBtn, declineBtn, cancelBtn, T17DialogBox.Symbols.Warning, true, bLocalizeMessage);
				T17DialogBox dialogBox2 = m_dialogBox;
				dialogBox2.OnConfirm = (T17DialogBox.DialogEvent)Delegate.Combine(dialogBox2.OnConfirm, new T17DialogBox.DialogEvent(OnConfirmed));
				m_dialogBox.Show();
			}
		}
	}

	private static void OnConfirmed()
	{
		m_dialogBox = null;
		if (OnNetworkErrorDialogDismissed != null)
		{
			OnNetworkErrorDialogDismissed();
		}
	}
}
