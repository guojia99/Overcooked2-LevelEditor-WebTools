#define ANALYTICS
using System;
using GameModes;
using Team17.Online;
using UnityEngine;
using UnityEngine.SceneManagement;

public class InGamePauseMenu : InGameMenuBehaviour
{
	public T17Text m_header;

	public GameObject m_buttons;

	public T17Button m_RestartButton;

	public T17Button m_CustomisationButton;

	[SerializeField]
	public T17Button m_gameModeSettingsButton;

	public T17Text m_PausedText;

	private T17DialogBox m_confirmDialog;

	[SerializeField]
	private UIPlayerRootMenu m_uiPlayers;

	[SerializeField]
	[AssignResource("GameModeUIData", Editorbility.Editable)]
	private GameModeUIData m_gameModeUIData;

	protected override void SingleTimeInitialize()
	{
		base.SingleTimeInitialize();
	}

	private void SetupForPlayer(int playerIdx)
	{
		bool flag = playerIdx == 0;
		m_buttons.SetActive(flag);
		m_PausedText.gameObject.SetActive(!flag);
		if (!flag)
		{
			string nonLocalizedText = Localization.Get("Text.Menu.PlayerPaused", new LocToken("[Name]", (playerIdx + 1).ToString()));
			m_PausedText.SetNonLocalizedText(nonLocalizedText);
		}
	}

	public override bool Show(GamepadUser currentGamer, BaseMenuBehaviour parent, GameObject invoker, bool hideInvoker = true)
	{
		if (!base.Show(currentGamer, parent, invoker, hideInvoker))
		{
			return false;
		}
		int num = 0;
		for (int i = 0; i < ClientUserSystem.m_Users.Count; i++)
		{
			User user = ClientUserSystem.m_Users._items[i];
			if (user.IsLocal)
			{
				if (user.GamepadUser == currentGamer)
				{
					SetupForPlayer(num);
					break;
				}
				num++;
			}
		}
		if (m_IPlayerManager != null)
		{
			m_IPlayerManager.EngagementChangeCallback += OnEngagementChanged;
		}
		if (m_header != null)
		{
			GameSession gameSession = GameUtils.GetGameSession();
			if (gameSession == null || SceneManager.GetActiveScene().name == gameSession.TypeSettings.WorldMapScene)
			{
				m_header.SetLocalisedTextCatchAll(MenuName);
			}
			else if (gameSession != null)
			{
				SceneDirectoryData sceneDirectory = gameSession.Progress.GetSceneDirectory();
				if (sceneDirectory != null)
				{
					SceneDirectoryData.SceneDirectoryEntry[] scenes = sceneDirectory.Scenes;
					int levelID = GameUtils.GetLevelID();
					if (levelID >= 0 && levelID < scenes.Length)
					{
						m_header.SetLocalisedTextCatchAll(scenes[levelID].Label);
					}
				}
			}
		}
		GameSession gameSession2 = GameUtils.GetGameSession();
		m_gameModeSettingsButton.gameObject.SetActive((ConnectionStatus.IsHost() || !ConnectionStatus.IsInSession()) && gameSession2 != null && m_gameModeUIData.m_gameModes[(int)gameSession2.GameModeKind].m_supportedSettings.Length > 0);
		return true;
	}

	public override bool Hide(bool restoreInvokerState = true, bool isTabSwitch = false)
	{
		if (m_IPlayerManager != null)
		{
			m_IPlayerManager.EngagementChangeCallback -= OnEngagementChanged;
		}
		if (m_uiPlayers != null)
		{
			m_uiPlayers.CloseAllPlayerMenus();
		}
		return base.Hide(restoreInvokerState, isTabSwitch);
	}

	protected override void Update()
	{
		base.Update();
	}

	public void SetPauseType(bool isWorldMapPause)
	{
		bool flag = !isWorldMapPause;
		if (flag && ConnectionStatus.IsInSession())
		{
			flag = ClientGameSetup.Mode == GameMode.Campaign && ConnectionStatus.IsHost();
		}
		m_RestartButton.gameObject.SetActive(flag);
		if (m_CustomisationButton != null)
		{
			m_CustomisationButton.gameObject.SetActive(isWorldMapPause);
		}
	}

	private void ShowRestartDialog()
	{
		if (m_confirmDialog == null)
		{
			m_confirmDialog = T17DialogBoxManager.GetDialog(false);
			if (m_confirmDialog != null)
			{
				m_confirmDialog.Initialize("Text.Pause.Restart.Title", "Text.Pause.Restart.Message", "Text.Button.Restart", null, "Text.Button.Cancel", T17DialogBox.Symbols.Unassigned);
				T17DialogBox confirmDialog = m_confirmDialog;
				confirmDialog.OnConfirm = (T17DialogBox.DialogEvent)Delegate.Combine(confirmDialog.OnConfirm, new T17DialogBox.DialogEvent(OnRestartConfirmed));
				T17DialogBox confirmDialog2 = m_confirmDialog;
				confirmDialog2.OnCancel = (T17DialogBox.DialogEvent)Delegate.Combine(confirmDialog2.OnCancel, new T17DialogBox.DialogEvent(HideConfirmDialog));
				m_confirmDialog.Show();
			}
		}
	}

	private void OnRestartConfirmed()
	{
		if (ConnectionStatus.IsHost() || !ConnectionStatus.IsInSession())
		{
			MultiplayerController multiplayerController = GameUtils.RequireManager<MultiplayerController>();
			multiplayerController.StopSynchronisation();
			ServerMessenger.LoadLevel(SceneManager.GetActiveScene().name, GameState.LoadKitchen, true, GameState.RunKitchen);
		}
		if (GameUtils.RequestManager<FlowControllerBase>() != null)
		{
			Analytics.LogEvent("Restart", 0L, Analytics.Flags.LevelName);
		}
	}

	public void OnRestartSelected()
	{
		ShowRestartDialog();
	}

	private void ShowQuitDialog()
	{
		if (m_confirmDialog == null)
		{
			m_confirmDialog = T17DialogBoxManager.GetDialog(false);
			if (m_confirmDialog != null)
			{
				m_confirmDialog.Initialize("Text.Pause.Quit.Title", "Text.Pause.Quit.Message", "Text.Button.Quit", null, "Text.Button.Cancel", T17DialogBox.Symbols.Unassigned);
				T17DialogBox confirmDialog = m_confirmDialog;
				confirmDialog.OnConfirm = (T17DialogBox.DialogEvent)Delegate.Combine(confirmDialog.OnConfirm, new T17DialogBox.DialogEvent(OnQuitConfirmed));
				T17DialogBox confirmDialog2 = m_confirmDialog;
				confirmDialog2.OnCancel = (T17DialogBox.DialogEvent)Delegate.Combine(confirmDialog2.OnCancel, new T17DialogBox.DialogEvent(HideConfirmDialog));
				m_confirmDialog.Show();
			}
		}
	}

	private void HideConfirmDialog()
	{
		if (m_confirmDialog != null)
		{
			m_confirmDialog.Hide();
			m_confirmDialog = null;
		}
	}

	private void OnQuitConfirmed()
	{
		if (GameUtils.RequestManager<FlowControllerBase>() != null)
		{
			Analytics.LogEvent(((!ConnectionStatus.IsHost()) ? "Client" : "Host") + " Quit", 0L, Analytics.Flags.LevelName);
		}
		MultiplayerController multiplayerController = GameUtils.RequireManager<MultiplayerController>();
		multiplayerController.StopSynchronisation();
		GameUtils.QuitLevel();
	}

	public void OnQuitSelected()
	{
		ShowQuitDialog();
	}

	private void OnEngagementChanged(EngagementSlot slot, GamepadUser prevUser, GamepadUser newUser)
	{
		if (slot == EngagementSlot.One && prevUser == null && newUser != null && m_IPlayerManager != null)
		{
			GamepadUser user = m_IPlayerManager.GetUser(slot);
			m_CurrentGamepadUser = user;
			m_CachedEventSystem = T17EventSystemsManager.Instance.GetEventSystemForGamepadUser(m_CurrentGamepadUser);
			if (m_CachedEventSystem != null && m_CachedEventSystem.GetLastRequestedSelectedGameobject() == null && m_BorderSelectables.selectOnUp != null)
			{
				m_CachedEventSystem.SetSelectedGameObject(m_BorderSelectables.selectOnUp.gameObject);
			}
		}
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		HideConfirmDialog();
	}

	private void OnApplicationFocus(bool focus)
	{
		if (focus && m_CachedEventSystem != null)
		{
			m_CachedEventSystem.SetSelectedGameObject(m_CachedEventSystem.GetLastRequestedSelectedGameobject());
		}
	}
}
