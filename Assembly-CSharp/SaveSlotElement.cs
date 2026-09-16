using System;
using System.Collections;
using System.Collections.Generic;
using GameModes;
using Team17.Online;
using UnityEngine;

public class SaveSlotElement : BaseMenuBehaviour
{
	protected struct SlotInfo
	{
		public GameProgress.GameProgressData m_saveData;

		public SceneDirectoryData m_sceneDirectory;

		[LevelIndex]
		public int m_firstSceneIndex;
	}

	[SerializeField]
	private T17Text m_levelName;

	[SerializeField]
	private T17Text m_starCount;

	[SerializeField]
	private int m_slotNum;

	[SerializeField]
	private GameObject m_validSaveSlot;

	[SerializeField]
	[AssignResource("NGPlusMarker", Editorbility.Editable)]
	private GameObject m_newGamePlusMarkerPrefab;

	private GameObject m_newGamePlusMarker;

	[SerializeField]
	private GameObject m_corruptSaveSlot;

	[SerializeField]
	private GameObject m_noSaveSlot;

	[SerializeField]
	[Range(1f, 20f)]
	private int m_maxStarCharacters = 3;

	protected SaveLoadResult? m_slotState;

	private SlotInfo m_slotInfo;

	protected SaveDialogMode m_mode;

	protected int m_dlcNum;

	private Suppressor m_suppressor;

	private IEnumerator m_slotClickedRoutine;

	private bool m_processingSlotTrigger;

	private bool m_bTriggeredLoad;

	private bool m_bSaveGameReady;

	private T17DialogBox m_overwriteDialog;

	private bool m_impendingForceClose;

	public int Slot
	{
		get
		{
			return m_slotNum;
		}
	}

	public SaveDialogMode Mode
	{
		get
		{
			return m_mode;
		}
		set
		{
			m_mode = value;
		}
	}

	public int DLC
	{
		get
		{
			return m_dlcNum;
		}
		set
		{
			m_dlcNum = value;
		}
	}

	public bool SaveGameReady
	{
		get
		{
			return m_bSaveGameReady;
		}
	}

	public override bool Show(GamepadUser _gamepadUser, BaseMenuBehaviour _parent, GameObject _invoker, bool _hideInvoker = true)
	{
		if (!base.Show(_gamepadUser, _parent, _invoker, _hideInvoker))
		{
			return false;
		}
		UpdateUI();
		m_bTriggeredLoad = false;
		m_bSaveGameReady = false;
		GameSession gameSession = GameUtils.GetGameSession();
		return true;
	}

	public IEnumerator LoadSlotData(int _dlcNum)
	{
		GameSession session = SelectSaveDialog.CreateFreshGameSessionForSlot(_dlcNum, m_slotNum);
		ReturnValue<SaveLoadResult> result = new ReturnValue<SaveLoadResult>();
		IEnumerator hasSaveRoutine = session.HasSaveFile(result);
		while (hasSaveRoutine.MoveNext())
		{
			yield return null;
		}
		m_slotState = result.Value;
		SaveLoadResult? slotState = m_slotState;
		if (slotState.GetValueOrDefault() == SaveLoadResult.Exists && slotState.HasValue)
		{
			IEnumerator sessionLoad = session.LoadSession();
			while (sessionLoad.MoveNext())
			{
				yield return null;
			}
			m_slotInfo.m_saveData = session.Progress.SaveableData;
			if (session.Progress.CanUnlockNewGamePlus(session.Progress.SaveableData))
			{
				session.Progress.UnlockNewGamePlus(session.Progress.SaveableData);
			}
		}
		else
		{
			m_slotInfo.m_saveData = null;
		}
		m_slotInfo.m_sceneDirectory = session.Progress.GetSceneDirectory();
		m_slotInfo.m_firstSceneIndex = session.Progress.FirstSceneIndex;
		if (base.gameObject.activeInHierarchy)
		{
			UpdateUI();
		}
	}

	public void UpdateUI()
	{
		if (m_slotState == SaveLoadResult.Corrupted)
		{
			UpdateUICorrupted();
		}
		else if (m_slotState == SaveLoadResult.NotExist || m_slotState == SaveLoadResult.NoSpace)
		{
			UpdateUIEmpty();
		}
		else if (m_slotInfo.m_saveData != null)
		{
			bool flag = m_slotInfo.m_saveData.IsNGPEnabledForAnyLevel();
			if (m_newGamePlusMarker != null)
			{
				m_newGamePlusMarker.SetActive(flag);
			}
			else if (m_newGamePlusMarkerPrefab != null && flag)
			{
				m_newGamePlusMarker = UnityEngine.Object.Instantiate(m_newGamePlusMarkerPrefab, m_validSaveSlot.transform);
				m_newGamePlusMarker.SetActive(true);
			}
			m_validSaveSlot.SetActive(true);
			m_corruptSaveSlot.SetActive(false);
			m_noSaveSlot.SetActive(false);
			int num = m_slotInfo.m_saveData.FarthestProgressedLevel();
			SceneDirectoryData sceneDirectory = m_slotInfo.m_sceneDirectory;
			string empty = string.Empty;
			empty = ((num == -1 || num >= sceneDirectory.Scenes.Length) ? sceneDirectory.Scenes[m_slotInfo.m_firstSceneIndex].Label : sceneDirectory.Scenes[num].Label);
			m_levelName.SetLocalisedTextCatchAll(empty);
			m_starCount.m_bNeedsLocalization = false;
			string text = m_slotInfo.m_saveData.GetStarTotal().ToString();
			text = text.PadLeft(m_maxStarCharacters, '0');
			m_starCount.text = text;
		}
		else
		{
			UpdateUIEmpty();
		}
	}

	protected void UpdateUICorrupted()
	{
		m_validSaveSlot.SetActive(false);
		m_corruptSaveSlot.SetActive(true);
		m_noSaveSlot.SetActive(false);
	}

	protected void UpdateUIEmpty()
	{
		m_validSaveSlot.SetActive(false);
		m_corruptSaveSlot.SetActive(false);
		m_noSaveSlot.SetActive(true);
	}

	public void OnSlotClicked()
	{
		m_slotClickedRoutine = TriggerSlotRoutine();
	}

	public IEnumerator TriggerSlotRoutine()
	{
		if (ConnectionModeSwitcher.GetStatus().GetProgress() == eConnectionModeSwitchProgress.InProgress)
		{
			yield break;
		}
		m_processingSlotTrigger = true;
		SuppressEventSystem();
		UpdateConnectionState();
		while (ConnectionModeSwitcher.GetStatus().GetProgress() != eConnectionModeSwitchProgress.Complete)
		{
			yield return null;
		}
		SaveLoadResult? slotState = m_slotState;
		if (!slotState.HasValue)
		{
			IEnumerator load = LoadSlotData(m_dlcNum);
			while (load.MoveNext())
			{
				yield return null;
			}
		}
		if (m_mode == SaveDialogMode.NewGame)
		{
			SaveLoadResult? slotState2 = m_slotState;
			if ((slotState2.GetValueOrDefault() == SaveLoadResult.Exists && slotState2.HasValue) || m_slotState == SaveLoadResult.Corrupted)
			{
				ReleaseActiveSuppression(true);
				bool? overwriteSave = null;
				ShowOverwriteDialog(delegate
				{
					overwriteSave = true;
					m_overwriteDialog = null;
				}, delegate
				{
					overwriteSave = false;
					m_overwriteDialog = null;
				});
				while (!overwriteSave.HasValue)
				{
					yield return null;
				}
				if (overwriteSave.Value)
				{
					SuppressEventSystem();
					bool deleted = false;
					SaveManager saveManager = GameUtils.RequireManager<SaveManager>();
					saveManager.DeleteSave(SaveMode.Main, m_slotNum, m_dlcNum, delegate
					{
						deleted = true;
					});
					while (!deleted)
					{
						yield return null;
					}
					ReleaseActiveSuppression();
				}
				else if (!overwriteSave.Value)
				{
					m_processingSlotTrigger = false;
					yield break;
				}
			}
			SuppressEventSystem();
			IEnumerator save = SaveNewGame(OnSaveGameReady);
			while (save.MoveNext())
			{
				yield return null;
			}
		}
		else
		{
			SuppressEventSystem();
			if (m_slotInfo.m_saveData != null || m_slotState == SaveLoadResult.Corrupted)
			{
				GameUtils.GetMetaGameProgress().SetLastSaveSlot(m_dlcNum, m_slotNum);
				IEnumerator loadRoutine = LoadSaveRoutine();
				while (loadRoutine.MoveNext())
				{
					yield return null;
				}
			}
			else
			{
				IEnumerator save2 = SaveNewGame(OnSaveGameReady);
				while (save2.MoveNext())
				{
					yield return null;
				}
			}
		}
		ReleaseActiveSuppression();
		m_processingSlotTrigger = false;
	}

	private void SuppressEventSystem()
	{
		if (m_suppressor != null)
		{
			return;
		}
		GamepadUser user = GameUtils.RequireManagerInterface<IPlayerManager>().GetUser(EngagementSlot.One);
		if (user != null)
		{
			T17EventSystem eventSystemForGamepadUser = T17EventSystemsManager.Instance.GetEventSystemForGamepadUser(user);
			if (eventSystemForGamepadUser != null)
			{
				m_suppressor = eventSystemForGamepadUser.Disable(this);
			}
		}
	}

	private void ReleaseActiveSuppression(bool _force = false)
	{
		if (m_suppressor == null)
		{
			return;
		}
		if (_force)
		{
			GamepadUser user = GameUtils.RequireManagerInterface<IPlayerManager>().GetUser(EngagementSlot.One);
			if (user != null)
			{
				T17EventSystem eventSystemForGamepadUser = T17EventSystemsManager.Instance.GetEventSystemForGamepadUser(user);
				if (eventSystemForGamepadUser != null)
				{
					eventSystemForGamepadUser.ReleaseSuppressor(m_suppressor);
					m_suppressor = null;
				}
			}
			if (m_suppressor != null)
			{
				m_suppressor.Release();
				m_suppressor = null;
			}
		}
		else
		{
			m_suppressor.Release();
			m_suppressor = null;
		}
	}

	private void UpdateConnectionState()
	{
		if (ConnectionModeSwitcher.GetRequestedConnectionState() == NetConnectionState.Server)
		{
			if (!UserSystemUtils.AnyRemoteUsers())
			{
				OnlineMultiplayerConnectionMode value = ((ConnectionStatus.CurrentConnectionMode() == OnlineMultiplayerConnectionMode.eInternet) ? OnlineMultiplayerConnectionMode.eInternet : OnlineMultiplayerConnectionMode.eNone);
				ConnectionModeSwitcher.RequestConnectionState(NetConnectionState.Offline, new OfflineOptions
				{
					hostUser = GameUtils.RequireManagerInterface<IPlayerManager>().GetUser(EngagementSlot.One),
					connectionMode = value
				});
			}
			else
			{
				ServerOptions serverOptions = (ServerOptions)ConnectionModeSwitcher.GetAgentData();
				serverOptions.visibility = OnlineMultiplayerSessionVisibility.eClosed;
				ConnectionModeSwitcher.RequestConnectionState(NetConnectionState.Server, serverOptions);
			}
		}
	}

	private IEnumerator LoadSaveRoutine()
	{
		GameSession session = SelectSaveDialog.CreateFreshGameSessionForSlot(m_dlcNum, m_slotNum);
		IEnumerator<SaveLoadResult?> loadSession = session.LoadSession();
		while (loadSession.MoveNext())
		{
			if (T17DialogBoxManager.HasAnyOpenDialogs())
			{
				ReleaseActiveSuppression();
			}
			yield return null;
		}
		SuppressEventSystem();
		if (loadSession.Current.Value == SaveLoadResult.Exists)
		{
			if (session.Progress.CanUnlockNewGamePlus(session.Progress.SaveableData))
			{
				session.Progress.UnlockNewGamePlus(session.Progress.SaveableData);
			}
			if (ConnectionStatus.IsInSession() && !ConnectionStatus.IsHost())
			{
				session.Progress.UseSlaveSlot = true;
			}
			OnSaveGameReady();
		}
		else if (loadSession.Current.Value == SaveLoadResult.NotExist)
		{
			IEnumerator save = SaveNewGame(OnSaveGameReady);
			while (save.MoveNext())
			{
				yield return null;
			}
		}
	}

	public void ServerLoadCampaign(GameSession session)
	{
		session.FillShownMetaDialogStatus();
		if (!session.Progress.LoadFirstScene || session.Progress.SaveData.IsLevelComplete(m_slotInfo.m_firstSceneIndex) || GameUtils.GetDebugConfig().m_skipTutorial)
		{
			ServerMessenger.SetupCoopSession(m_dlcNum, session.Progress.SaveData, session.m_shownMetaDialogs, session.GameModeSessionConfig);
			ServerGameSetup.Mode = GameMode.Campaign;
			ServerMessenger.LoadLevel(session.TypeSettings.WorldMapScene, GameState.CampaignMap, true, GameState.RunMapUnfoldRoutine);
		}
		else
		{
			SessionConfig sessionConfig = new SessionConfig();
			sessionConfig.Copy(session.GameModeSessionConfig);
			sessionConfig.m_kind = Kind.Campaign;
			ServerMessenger.SetupCoopSession(m_dlcNum, session.Progress.SaveData, session.m_shownMetaDialogs, sessionConfig);
			LoadFirstLevel();
		}
		if (T17FrontendFlow.Instance != null)
		{
			T17FrontendFlow.Instance.HideWaitingForPlayers();
		}
		m_bTriggeredLoad = true;
	}

	private void LoadFirstLevel()
	{
		int firstSceneIndex = m_slotInfo.m_firstSceneIndex;
		SceneDirectoryData.SceneDirectoryEntry sceneDirectoryEntry = m_slotInfo.m_sceneDirectory.Scenes[firstSceneIndex];
		SceneDirectoryData.PerPlayerCountDirectoryEntry sceneVarient = sceneDirectoryEntry.GetSceneVarient(ClientUserSystem.m_Users.Count);
		if (sceneVarient != null)
		{
			string sceneName = sceneVarient.SceneName;
			if (sceneName != string.Empty)
			{
				GameSession.GameLevelSettings gameLevelSettings = new GameSession.GameLevelSettings();
				gameLevelSettings.SceneDirectoryVarientEntry = sceneVarient;
				ServerGameSetup.Mode = GameMode.Campaign;
				ServerMessenger.LoadLevel((uint)firstSceneIndex, (uint)ClientUserSystem.m_Users.Count, GameState.LoadKitchen, GameState.RunKitchen);
				return;
			}
		}
		Vector2 vector = new Vector2(0.5f * (float)Camera.main.pixelWidth, 0.5f * (float)Camera.main.pixelHeight);
	}

	private IEnumerator SaveNewGame(CallbackVoid _onSaveSuccess)
	{
		GameSession session = SelectSaveDialog.CreateFreshGameSessionForSlot(m_dlcNum, m_slotNum);
		if (ConnectionStatus.IsInSession() && !ConnectionStatus.IsHost())
		{
			session.Progress.UseSlaveSlot = true;
		}
		SaveLoadResult? saveResult = null;
		SaveSystemCallback callback = delegate(SaveSystemStatus _status)
		{
			if (_status.Status == SaveSystemStatus.SaveStatus.Complete)
			{
				saveResult = _status.Result;
			}
		};
		GameUtils.RequireManager<SaveManager>().RegisterOnIdle(delegate
		{
			session.SaveSession(callback);
		});
		while (!saveResult.HasValue)
		{
			if (T17DialogBoxManager.HasAnyOpenDialogs())
			{
				ReleaseActiveSuppression();
			}
			else
			{
				SuppressEventSystem();
			}
			yield return null;
		}
		SuppressEventSystem();
		if (saveResult != SaveLoadResult.Cancel || !saveResult.HasValue)
		{
			_onSaveSuccess();
		}
	}

	private void OnSaveGameReady()
	{
		if (m_impendingForceClose)
		{
			return;
		}
		GameUtils.GetMetaGameProgress().SetLastSaveSlot(m_dlcNum, m_slotNum);
		GameProgress.HighScores highScores = new GameProgress.HighScores();
		GameSession gameSession = GameUtils.GetGameSession();
		gameSession.Progress.GetLocalScores(ref highScores);
		gameSession.HighScoreRepository.SetScoresForMachine(ClientUserSystem.s_LocalMachineId, gameSession.DLC, highScores);
		if (ConnectionStatus.IsInSession())
		{
			if (ConnectionStatus.IsHost())
			{
				GameStateMessage.ClientSavePayload clientSavePayload = new GameStateMessage.ClientSavePayload();
				clientSavePayload.Initialise(m_dlcNum);
				UserSystemUtils.ChangeGameState(GameState.SelectCampaignMapSave, clientSavePayload);
			}
			else
			{
				ClientMessenger.HighScores(highScores, gameSession.DLC);
			}
			ClientMessenger.GameState(GameState.LoadedCampaignMapSave);
			if (T17FrontendFlow.Instance != null)
			{
				T17FrontendFlow.Instance.ShowWaitingForPlayers();
			}
		}
		else
		{
			ServerLoadCampaign(gameSession);
		}
		m_bSaveGameReady = true;
	}

	protected override void Update()
	{
		base.Update();
		if (m_slotClickedRoutine != null && !m_slotClickedRoutine.MoveNext())
		{
			m_slotClickedRoutine = null;
		}
	}

	public void ShowOverwriteDialog(T17DialogBox.DialogEvent _confirmCallback, T17DialogBox.DialogEvent _cancelCallback)
	{
		m_overwriteDialog = T17DialogBoxManager.GetDialog(false);
		if (m_overwriteDialog != null)
		{
			m_overwriteDialog.Initialize("Text.OverwriteSlot.Title", "Text.OverwriteSlot.Body", "Text.Button.Confirm", null, "Text.Button.Cancel");
			T17DialogBox overwriteDialog = m_overwriteDialog;
			overwriteDialog.OnConfirm = (T17DialogBox.DialogEvent)Delegate.Combine(overwriteDialog.OnConfirm, _confirmCallback);
			T17DialogBox overwriteDialog2 = m_overwriteDialog;
			overwriteDialog2.OnCancel = (T17DialogBox.DialogEvent)Delegate.Combine(overwriteDialog2.OnCancel, _cancelCallback);
			m_overwriteDialog.Show();
		}
		else
		{
			_cancelCallback();
		}
	}

	public void InformImpendingForceClose()
	{
		m_impendingForceClose = true;
	}

	public bool CanHide()
	{
		if (m_processingSlotTrigger)
		{
			bool flag = GameUtils.RequireManager<SaveManager>().HasActiveInputDialog();
			if (!(flag | (m_overwriteDialog != null && m_overwriteDialog.IsActive)))
			{
				return false;
			}
		}
		return true;
	}

	public bool CleanUp()
	{
		if (m_slotClickedRoutine != null)
		{
			if (m_overwriteDialog != null)
			{
				m_overwriteDialog.Cancel();
			}
			GameUtils.RequireManager<SaveManager>().CancelActiveInputDialog();
			if (m_slotClickedRoutine.MoveNext())
			{
				return false;
			}
		}
		m_slotClickedRoutine = null;
		m_slotState = null;
		m_slotInfo.m_saveData = null;
		return true;
	}

	public override bool Hide(bool restoreInvokerState = true, bool isTabSwitch = false)
	{
		if (!CanHide())
		{
			return false;
		}
		if (!CleanUp())
		{
			return false;
		}
		if (!base.Hide(restoreInvokerState, isTabSwitch))
		{
			return false;
		}
		ReleaseActiveSuppression();
		m_impendingForceClose = false;
		return true;
	}
}
