using System.Collections;

public class RestartQuitSplashFlowroutine
{
	private PauseMenuManager m_pauseManager;

	private PopupGUI m_popupGUI;

	private string m_frontEndName;

	private ILogicalButton m_resetButton;

	private ILogicalButton m_quitButton;

	public RestartQuitSplashFlowroutine(PauseMenuManager _pauseManager, string _frontEndName, PopupGUI _popupGUI)
	{
		m_pauseManager = _pauseManager;
		m_frontEndName = _frontEndName;
		m_popupGUI = _popupGUI;
		m_resetButton = PlayerInputLookup.GetUIButton(PlayerInputLookup.LogicalButtonID.ResetButton);
		m_quitButton = PlayerInputLookup.GetUIButton(PlayerInputLookup.LogicalButtonID.QuitButton);
	}

	public IEnumerator Run()
	{
		m_pauseManager.enabled = false;
		m_popupGUI.enabled = true;
		while (true)
		{
			if (m_resetButton.IsDown())
			{
				GameUtils.LoadScene(m_frontEndName);
			}
			if (m_quitButton.IsDown())
			{
				GameUtils.LoadScene(m_frontEndName);
			}
			yield return null;
		}
	}
}
