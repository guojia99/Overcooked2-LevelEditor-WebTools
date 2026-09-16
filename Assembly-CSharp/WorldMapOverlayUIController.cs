using System;
using GameModes;
using UnityEngine;

public class WorldMapOverlayUIController : UIControllerBase
{
	[SerializeField]
	[AssignResource("GameModeUIData", Editorbility.Editable)]
	private GameModeUIData m_gameModeUIData;

	[Header("Mode UI")]
	[SerializeField]
	private T17Text m_gameModeUIText;

	[Header("Star UI")]
	[SerializeField]
	private GameObject m_starCountUIRoot;

	[SerializeField]
	private T17Text m_starCountUIText;

	private IPlayerManager m_playerManager;

	private ILogicalButton m_changeModeButton;

	private void Start()
	{
		GameSession gameSession = GameUtils.GetGameSession();
		m_starCountUIRoot.SetActive(gameSession.GameModeKind == Kind.Campaign);
		m_gameModeUIText.SetLocalisedTextCatchAll(m_gameModeUIData.m_gameModes[(int)gameSession.GameModeKind].m_nameLocalisationKey);
		m_starCountUIText.text = gameSession.Progress.GetStarTotal().ToString().PadLeft(3, '0');
		gameSession.OnGameModeSessionConfigChanged = (OnSessionConfigChanged)Delegate.Combine(gameSession.OnGameModeSessionConfigChanged, new OnSessionConfigChanged(OnGameModeSessionConfigChanged));
		m_playerManager = GameUtils.RequestManagerInterface<IPlayerManager>();
		m_changeModeButton = PlayerInputLookup.GetUIButton(PlayerInputLookup.LogicalButtonID.UIChangeMode);
	}

	private void OnDestroy()
	{
		GameSession gameSession = GameUtils.GetGameSession();
		gameSession.OnGameModeSessionConfigChanged = (OnSessionConfigChanged)Delegate.Remove(gameSession.OnGameModeSessionConfigChanged, new OnSessionConfigChanged(OnGameModeSessionConfigChanged));
	}

	private void Update()
	{
		if ((ConnectionStatus.IsHost() || !ConnectionStatus.IsInSession()) && !T17DialogBoxManager.HasAnyOpenDialogs() && !m_playerManager.IsWarningActive(PlayerWarning.Disengaged) && m_changeModeButton.JustPressed() && T17InGameFlow.Instance != null && T17InGameFlow.Instance.m_Rootmenu.GetCurrentOpenMenu() == null)
		{
			m_changeModeButton.ClaimPressEvent();
			m_changeModeButton.ClaimReleaseEvent();
			T17InGameFlow.Instance.m_Rootmenu.OpenInGameMenu(InGameRootMenu.IngameMenuTypeToOpen.GameMode);
		}
	}

	private void OnGameModeSessionConfigChanged(SessionConfig config)
	{
		m_starCountUIRoot.SetActive(config.m_kind == Kind.Campaign);
		m_gameModeUIText.SetLocalisedTextCatchAll(m_gameModeUIData.m_gameModes[(int)config.m_kind].m_nameLocalisationKey);
	}
}
