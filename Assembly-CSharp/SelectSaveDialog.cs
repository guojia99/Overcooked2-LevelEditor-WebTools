using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SelectSaveDialog : FrontendMenuBehaviour
{
	[Header("Select Save Dialog")]
	[SerializeField]
	private string m_newGameHeader;

	[SerializeField]
	private string m_loadHeader;

	[SerializeField]
	private T17Text m_header;

	[SerializeField]
	private SaveSlotElement[] m_saveElements;

	[SerializeField]
	public GameObject m_PleaseWaitMenu;

	[SerializeField]
	public T17Text m_Timer;

	[SerializeField]
	public GameObject m_DontSaveButton;

	private SaveDialogMode m_mode;

	private int m_dlcNum;

	private IEnumerator m_slotUpdate;

	private T17EventSystem m_eventSystem;

	private Suppressor m_suppressor;

	private GamepadEngagementManager m_gamepadEngagementManager;

	private Suppressor m_engagementSuppressor;

	private GenericVoid m_closeSuccessCallback;

	[SerializeField]
	private GameObject m_legend;

	[SerializeField]
	private T17Text m_info;

	private bool m_pendingForceClose;

	private static HighScoreRepository s_cachedHighScores;

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

	protected override void SingleTimeInitialize()
	{
		base.SingleTimeInitialize();
		m_gamepadEngagementManager = GameUtils.RequireManager<GamepadEngagementManager>();
	}

	public override bool Show(GamepadUser _currentGamer, BaseMenuBehaviour _parent, GameObject _invoker, bool _hideInvoker = true)
	{
		if (!base.Show(_currentGamer, _parent, _invoker, _hideInvoker))
		{
			return false;
		}
		GameSession gameSession = GameUtils.GetGameSession();
		s_cachedHighScores = ((!(gameSession != null)) ? null : gameSession.HighScoreRepository);
		if (m_gamepadEngagementManager != null)
		{
			m_engagementSuppressor = m_gamepadEngagementManager.Suppressor.AddSuppressor(this);
		}
		if (T17FrontendFlow.Instance != null)
		{
			T17FrontendFlow.Instance.BlockFocusKitchen = true;
		}
		if (null != m_DontSaveButton)
		{
			m_DontSaveButton.SetActive(false);
		}
		m_header.SetLocalisedTextCatchAll((m_mode != SaveDialogMode.NewGame) ? m_loadHeader : m_newGameHeader);
		IPlayerManager playerManager = GameUtils.RequestManagerInterface<IPlayerManager>();
		GamepadUser user = playerManager.GetUser(EngagementSlot.One);
		m_eventSystem = T17EventSystemsManager.Instance.GetEventSystemForGamepadUser(user);
		if (m_eventSystem == null)
		{
			playerManager.EngagementChangeCallback += OnEngagementChanged;
		}
		m_slotUpdate = ShowSlotElements(m_mode, m_dlcNum, _currentGamer, this, _invoker, _hideInvoker);
		InviteMonitor.InviteJoinComplete = (GenericVoid)Delegate.Combine(InviteMonitor.InviteJoinComplete, new GenericVoid(OnInviteJoinComplete));
		if ((bool)m_legend)
		{
			m_legend.SetActive(false);
		}
		if (m_info != null)
		{
			DLCFrontendData dLCFrontendData = GetDLCFrontendData(m_dlcNum);
			string replaceWith = Localization.Get((!(dLCFrontendData == null)) ? dLCFrontendData.m_NameLocalizationKey : "Text.Menu.Story").ToUpperInvariant();
			string nonLocalizedText = Localization.Get("Text.Menu.SaveSlotInfo", new LocToken("[DLCNAME]", replaceWith));
			m_info.SetNonLocalizedText(nonLocalizedText);
		}
		m_pendingForceClose = false;
		return true;
	}

	private DLCFrontendData GetDLCFrontendData(int dlcNum)
	{
		DLCManager dLCManager = GameUtils.RequestManager<DLCManager>();
		if (dLCManager == null)
		{
			return null;
		}
		List<DLCFrontendData> allDlc = dLCManager.AllDlc;
		for (int i = 0; i < allDlc.Count; i++)
		{
			if (allDlc[i].m_DLCID == m_dlcNum)
			{
				return allDlc[i];
			}
		}
		return null;
	}

	private void OnEngagementChanged(EngagementSlot _s, GamepadUser _p, GamepadUser _n)
	{
		IPlayerManager playerManager = GameUtils.RequestManagerInterface<IPlayerManager>();
		GamepadUser user = playerManager.GetUser(EngagementSlot.One);
		T17EventSystem eventSystemForGamepadUser = T17EventSystemsManager.Instance.GetEventSystemForGamepadUser(user);
		if (!(eventSystemForGamepadUser == null))
		{
			playerManager.EngagementChangeCallback -= OnEngagementChanged;
			m_eventSystem = eventSystemForGamepadUser;
			m_eventSystem.SetSelectedGameObject(m_saveElements[0].gameObject);
		}
	}

	protected IEnumerator ShowSlotElements(SaveDialogMode _mode, int _dlcNum, GamepadUser _currentGamer, BaseMenuBehaviour _parent, GameObject _invoker, bool _hideInvoker)
	{
		if (m_eventSystem != null)
		{
			m_suppressor = m_eventSystem.Disable(this);
		}
		for (int i = 0; i < m_saveElements.Length; i++)
		{
			m_saveElements[i].Hide(false);
		}
		for (int j = 0; j < m_saveElements.Length; j++)
		{
			IEnumerator slotLoad = m_saveElements[j].LoadSlotData(_dlcNum);
			while (slotLoad.MoveNext())
			{
				yield return null;
			}
		}
		if (null != m_DontSaveButton)
		{
			m_DontSaveButton.SetActive(true);
		}
		T17FrontendFlow.Instance.StartClientCountdown();
		for (int k = 0; k < m_saveElements.Length; k++)
		{
			m_saveElements[k].Mode = _mode;
			m_saveElements[k].DLC = _dlcNum;
			m_saveElements[k].Show(_currentGamer, this, _invoker, _hideInvoker);
		}
		if (m_eventSystem != null)
		{
			ReleaseHolder();
			m_eventSystem.SetSelectedGameObject(m_saveElements[0].gameObject);
		}
		GameSession session = CreateFreshGameSessionForSlot(DLC, -1);
		if (ConnectionStatus.IsInSession() && !ConnectionStatus.IsHost() && session != null)
		{
			session.Progress.UseSlaveSlot = false;
		}
	}

	protected override void Update()
	{
		base.Update();
		if (ConnectionStatus.IsInSession() && !ConnectionStatus.IsHost())
		{
			if (null != m_Timer && T17FrontendFlow.Instance != null)
			{
				m_Timer.text = Mathf.FloorToInt(T17FrontendFlow.Instance.ClientCountdown + 0.99f).ToString();
			}
			if (T17FrontendFlow.Instance.ClientCountdownRunning && T17FrontendFlow.Instance.ClientCountdown <= 0f)
			{
				ClientMessenger.GameState(GameState.LoadedCampaignMapSave);
				ForceClose();
			}
		}
		if (m_slotUpdate != null && !m_slotUpdate.MoveNext())
		{
			m_slotUpdate = null;
		}
		if (m_pendingForceClose)
		{
			Debug.Log("Retrying force close...");
			base.Close();
		}
	}

	public void OnDontSaveSelected()
	{
		ClientMessenger.GameState(GameState.LoadedCampaignMapSave);
		GameSession gameSession = GameUtils.GetGameSession();
		if (gameSession != null)
		{
			gameSession.Progress.UseSlaveSlot = false;
		}
		ForceClose();
		if (T17FrontendFlow.Instance != null)
		{
			T17FrontendFlow.Instance.ShowWaitingForPlayers();
		}
	}

	public bool CanHide()
	{
		for (int i = 0; i < m_saveElements.Length; i++)
		{
			if (!m_saveElements[i].CanHide())
			{
				return false;
			}
		}
		return true;
	}

	public void HandleDisconnection(GenericVoid _disconnectionHandledCallback = null)
	{
		m_closeSuccessCallback = (GenericVoid)Delegate.Combine(m_closeSuccessCallback, _disconnectionHandledCallback);
		ForceClose();
	}

	public override bool Hide(bool restoreInvokerState = true, bool isTabSwitch = false)
	{
		if (!CanHide())
		{
			return false;
		}
		for (int i = 0; i < m_saveElements.Length; i++)
		{
			SaveSlotElement saveSlotElement = m_saveElements[i];
			if (!saveSlotElement.CleanUp())
			{
				return false;
			}
		}
		if (T17FrontendFlow.Instance != null)
		{
			T17FrontendFlow.Instance.HideWaitingForPlayers();
		}
		if (!base.Hide(restoreInvokerState, isTabSwitch))
		{
			return false;
		}
		for (int j = 0; j < m_saveElements.Length; j++)
		{
			m_saveElements[j].Hide(restoreInvokerState, isTabSwitch);
		}
		if (null != m_DontSaveButton)
		{
			m_DontSaveButton.SetActive(false);
		}
		if (m_closeSuccessCallback != null)
		{
			m_closeSuccessCallback();
			m_closeSuccessCallback = null;
		}
		ReleaseHolder();
		if (T17FrontendFlow.Instance != null)
		{
			T17FrontendFlow.Instance.BlockFocusKitchen = false;
		}
		if (m_engagementSuppressor != null)
		{
			m_engagementSuppressor.Release();
			m_engagementSuppressor = null;
		}
		if ((bool)m_legend)
		{
			m_legend.SetActive(true);
		}
		InviteMonitor.InviteJoinComplete = (GenericVoid)Delegate.Remove(InviteMonitor.InviteJoinComplete, new GenericVoid(OnInviteJoinComplete));
		m_pendingForceClose = false;
		s_cachedHighScores = null;
		return true;
	}

	protected void ReleaseHolder()
	{
		if (m_eventSystem != null && m_suppressor != null)
		{
			m_eventSystem.ReleaseSuppressor(m_suppressor);
			m_suppressor = null;
		}
	}

	public override void Close()
	{
		if (!T17DialogBoxManager.HasAnyOpenDialogs() && CanHide())
		{
			ForceClose();
		}
	}

	private void ForceClose()
	{
		IPlayerManager playerManager = GameUtils.RequestManagerInterface<IPlayerManager>();
		if (playerManager != null)
		{
			playerManager.EngagementChangeCallback -= OnEngagementChanged;
		}
		if ((bool)m_legend)
		{
			m_legend.SetActive(true);
		}
		m_pendingForceClose = true;
		for (int i = 0; i < m_saveElements.Length; i++)
		{
			m_saveElements[i].InformImpendingForceClose();
		}
		base.Close();
	}

	private void OnInviteJoinComplete()
	{
		ForceClose();
	}

	public IEnumerator LoadSlot(int _dlcNum, int _slotNum)
	{
		if (m_gamepadEngagementManager != null)
		{
			m_engagementSuppressor = m_gamepadEngagementManager.Suppressor.AddSuppressor(this);
		}
		for (int i = 0; i < m_saveElements.Length; i++)
		{
			SaveSlotElement saveSlot = m_saveElements[i];
			if (saveSlot.Slot == _slotNum)
			{
				saveSlot.Mode = SaveDialogMode.LoadGame;
				saveSlot.DLC = _dlcNum;
				IEnumerator loadSlot = saveSlot.TriggerSlotRoutine();
				while (loadSlot.MoveNext())
				{
					yield return null;
				}
				break;
			}
		}
		if (m_engagementSuppressor != null)
		{
			m_engagementSuppressor.Release();
			m_engagementSuppressor = null;
		}
	}

	public bool LoadReadySlot()
	{
		for (int i = 0; i < m_saveElements.Length; i++)
		{
			SaveSlotElement saveSlotElement = m_saveElements[i];
			if (saveSlotElement.SaveGameReady)
			{
				saveSlotElement.ServerLoadCampaign(GameUtils.GetGameSession());
				return true;
			}
		}
		return false;
	}

	public static GameSession CreateFreshGameSessionForSlot(int _dlcNum, int _slotNum)
	{
		GameSession gameSession = GameUtils.GetGameSession();
		GameSession gameSession2 = T17FrontendFlow.Instance.StartEmptySession(GameSession.GameType.Cooperative, _dlcNum);
		gameSession2.SaveSlot = _slotNum;
		if (s_cachedHighScores != null && s_cachedHighScores.DLC == _dlcNum)
		{
			gameSession2.HighScoreRepository.Fill(s_cachedHighScores, false);
		}
		s_cachedHighScores = gameSession2.HighScoreRepository;
		return gameSession2;
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		InviteMonitor.InviteJoinComplete = (GenericVoid)Delegate.Remove(InviteMonitor.InviteJoinComplete, new GenericVoid(OnInviteJoinComplete));
		ForceClose();
	}
}
