using System;

public class ExceptionDialogHandler : IExceptionDisplayer
{
	private T17DialogBox m_dialogBox;

	public void Initialize()
	{
	}

	public void OnGUI()
	{
	}

	public void Display(string exceptionString, string stackTrace, bool bJustOccured)
	{
		if (!bJustOccured || m_dialogBox != null)
		{
			return;
		}
		PlayerManager playerManager = GameUtils.RequireManager<PlayerManager>();
		GamepadUser user = playerManager.GetUser(EngagementSlot.One);
		if (!(T17EventSystemsManager.Instance.GetEventSystemForGamepadUser(user) == null))
		{
			m_dialogBox = T17DialogBoxManager.GetDialog(false);
			if (m_dialogBox != null)
			{
				string text = stackTrace.Substring(0, stackTrace.IndexOf('\n'));
				m_dialogBox.Initialize("Exception", exceptionString + "\n" + text, "OK", null, null, T17DialogBox.Symbols.Error, false, false);
				T17DialogBox dialogBox = m_dialogBox;
				dialogBox.OnConfirm = (T17DialogBox.DialogEvent)Delegate.Combine(dialogBox.OnConfirm, new T17DialogBox.DialogEvent(OnConfirmDialog));
				m_dialogBox.Show();
				SetGamePaused(true);
			}
		}
	}

	private void OnConfirmDialog()
	{
		m_dialogBox = null;
		SetGamePaused(false);
	}

	private void SetGamePaused(bool bSetPaused)
	{
		if (!ConnectionStatus.IsInSession())
		{
			TimeManager timeManager = GameUtils.RequestManager<TimeManager>();
			if (timeManager != null)
			{
				timeManager.SetPaused(TimeManager.PauseLayer.Main, bSetPaused, this);
			}
		}
	}
}
