using GameModes;
using UnityEngine;

public class GameModeSettingElementUIController : UIControllerBase
{
	[HideInInspector]
	private ModeSettingUIData m_uiData;

	private SettingKind m_kind;

	[SerializeField]
	private T17Text m_title;

	[SerializeField]
	private T17Toggle m_enableToggle;

	private bool m_ready;

	private void Start()
	{
	}

	public void SetData(SettingKind kind, ModeSettingUIData uiData, T17EventSystem eventSystem)
	{
		m_ready = false;
		m_kind = kind;
		m_uiData = uiData;
		m_title.SetLocalisedTextCatchAll(m_uiData.m_nameLocalisationKey);
		GameSession gameSession = GameUtils.GetGameSession();
		m_enableToggle.SetEventSystem(eventSystem);
		m_enableToggle.isOn = gameSession.GetGameModeSetting(m_kind);
		m_ready = true;
	}

	public void OnToggle(bool value)
	{
		if (m_ready)
		{
			GameSession gameSession = GameUtils.GetGameSession();
			gameSession.SetGameModeSetting(m_kind, m_enableToggle.isOn);
		}
	}
}
