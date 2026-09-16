using GameModes;
using UnityEngine;

public class GameModeDialogueElementUIController : UIControllerBase
{
	[HideInInspector]
	private ModeUIData m_uiData;

	private InGameMenuBehaviour m_parent;

	private Kind m_kind;

	[SerializeField]
	private T17Text m_name;

	[SerializeField]
	private T17Text m_description;

	[SerializeField]
	private T17Image m_image;

	[SerializeField]
	private T17Button m_selectButton;

	private void Start()
	{
	}

	public void SetData(InGameMenuBehaviour parent, Kind kind, ModeUIData uiData)
	{
		m_parent = parent;
		m_kind = kind;
		m_uiData = uiData;
		m_name.SetLocalisedTextCatchAll(m_uiData.m_nameLocalisationKey);
		m_description.SetLocalisedTextCatchAll(m_uiData.m_descriptionLocalisationKey);
		m_image.sprite = m_uiData.m_previewImage;
	}

	public void OnModeSelect()
	{
		GameSession gameSession = GameUtils.GetGameSession();
		gameSession.GameModeKind = m_kind;
		gameSession.CommitGameModeSessionConfig();
		m_parent.Close();
	}
}
