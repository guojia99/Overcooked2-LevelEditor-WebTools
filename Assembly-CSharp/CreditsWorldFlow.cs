using System;
using System.Collections;
using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class CreditsWorldFlow : Manager
{
	private GameState m_state = GameState.LoadKitchen;

	private IEnumerator m_levelRoutine;

	[SerializeField]
	private Animator m_animator;

	[SerializeField]
	private string m_start = "Start";

	[SerializeField]
	private string m_finished = "Finished";

	private int m_startId;

	private int m_finishedId;

	private GameObject m_skipCanvas;

	[SerializeField]
	[AssignResource("CutsceneSkipUI", Editorbility.NonEditable)]
	private GameObject m_skipUIPrefab;

	private bool m_isFinished;

	private bool m_skipped;

	private IEnumerator m_skipRoutine;

	private const float c_skipHoldDuration = 1f;

	private const float c_skipPromptTimout = 2f;

	private void Awake()
	{
		m_startId = Animator.StringToHash(m_start);
		m_finishedId = Animator.StringToHash(m_finished);
		Mailbox.Client.RegisterForMessageType(MessageType.GameState, OnGameStateChanged);
		ClientMessenger.GameState(GameState.RanLevelIntro);
		DisconnectionHandler.LocalDisconnectionEvent = (GenericVoid<OnlineMultiplayerReturnCode<OnlineMultiplayerSessionDisconnectionResult>>)Delegate.Combine(DisconnectionHandler.LocalDisconnectionEvent, new GenericVoid<OnlineMultiplayerReturnCode<OnlineMultiplayerSessionDisconnectionResult>>(OnLocalDisconnection));
		if (ConnectionStatus.IsHost() || !ConnectionStatus.IsInSession())
		{
			if (m_skipUIPrefab != null)
			{
				GameObject skipCanvas = GameUtils.InstantiateUIController(m_skipUIPrefab, "UICanvas");
				m_skipCanvas = skipCanvas;
				m_skipCanvas.SetActive(false);
			}
			m_skipRoutine = RunSkipRoutine();
		}
	}

	private void OnLocalDisconnection(OnlineMultiplayerReturnCode<OnlineMultiplayerSessionDisconnectionResult> result)
	{
		ConnectionModeSwitcher.RequestConnectionState(NetConnectionState.Offline, null, delegate
		{
			ServerGameSetup.Mode = GameMode.OnlineKitchen;
			ServerMessenger.LoadLevel("StartScreen", GameState.MainMenu, true);
		});
	}

	private void OnDestroy()
	{
		Mailbox.Client.UnregisterForMessageType(MessageType.GameState, OnGameStateChanged);
		DisconnectionHandler.LocalDisconnectionEvent = (GenericVoid<OnlineMultiplayerReturnCode<OnlineMultiplayerSessionDisconnectionResult>>)Delegate.Remove(DisconnectionHandler.LocalDisconnectionEvent, new GenericVoid<OnlineMultiplayerReturnCode<OnlineMultiplayerSessionDisconnectionResult>>(OnLocalDisconnection));
	}

	private void OnGameStateChanged(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
	{
		GameStateMessage gameStateMessage = (GameStateMessage)message;
		GameState state = gameStateMessage.m_State;
		if (state == GameState.InLevel)
		{
			m_levelRoutine = RunLevel();
			StartCoroutine(m_levelRoutine);
		}
	}

	private void Update()
	{
		if (!ConnectionStatus.IsHost() && ConnectionStatus.IsInSession())
		{
			return;
		}
		switch (m_state)
		{
		case GameState.LoadKitchen:
			if (AreAllUsersInGameState(GameState.RanLevelIntro))
			{
				ChangeGameState(GameState.InLevel);
			}
			break;
		case GameState.InLevel:
			if (!m_skipped && m_skipRoutine != null && !m_skipRoutine.MoveNext())
			{
				m_skipped = true;
			}
			if (AreAllUsersInGameState(GameState.RanLevelOutro) || m_skipped)
			{
				m_state = GameState.RanLevelOutro;
				MultiplayerController multiplayerController = GameUtils.RequireManager<MultiplayerController>();
				multiplayerController.StopSynchronisation();
				GameSession gameSession = GameUtils.GetGameSession();
				gameSession.FillShownMetaDialogStatus();
				ServerMessenger.GameProgressData(gameSession.Progress.SaveData, gameSession.m_shownMetaDialogs);
				ServerMessenger.LoadLevel(gameSession.TypeSettings.WorldMapScene, GameState.CampaignMap, true, GameState.RunMapUnfoldRoutine);
			}
			break;
		}
	}

	private IEnumerator RunLevel()
	{
		m_animator.SetTrigger(m_startId);
		while (true)
		{
			if (base.enabled)
			{
				m_isFinished = m_animator.GetBool(m_finishedId);
				if (HasFinished())
				{
					break;
				}
			}
			yield return null;
		}
		ClientMessenger.GameState(GameState.RanLevelOutro);
	}

	private IEnumerator RunSkipRoutine()
	{
		if (m_skipCanvas != null)
		{
			ILogicalButton startSkip = PlayerInputLookup.GetAnyButton(PlayerInputLookup.LogicalButtonID.UISkip, PadSide.Both);
			while (!startSkip.JustReleased())
			{
				yield return null;
			}
		}
		ILogicalButton rawSkipButton = PlayerInputLookup.GetAnyButton(PlayerInputLookup.LogicalButtonID.UISkip, PadSide.Both);
		TimedLogicalButton skipButton = new TimedLogicalButton(rawSkipButton, TimedLogicalButton.Condition.HeldLonger, 1f);
		if (m_skipCanvas != null)
		{
			m_skipCanvas.SetActive(true);
			TimedInputUIController timedInputUIController = m_skipCanvas.gameObject.RequestComponentRecursive<TimedInputUIController>();
			timedInputUIController.SetDisplayInput(skipButton, 1f);
		}
		bool skipped = false;
		while (!skipped)
		{
			if (m_skipCanvas != null)
			{
				if (!m_skipCanvas.activeSelf)
				{
					while (!rawSkipButton.IsDown())
					{
						yield return null;
					}
				}
				m_skipCanvas.SetActive(true);
				IEnumerator timoutRoutine = null;
				while (!skipped && (timoutRoutine == null || timoutRoutine.MoveNext()))
				{
					if (timoutRoutine == null)
					{
						if (!rawSkipButton.IsDown())
						{
							timoutRoutine = CoroutineUtils.TimerRoutine(2f, LayerMask.NameToLayer("UI"));
						}
					}
					else if (rawSkipButton.IsDown())
					{
						timoutRoutine = null;
					}
					if (skipButton.JustPressed())
					{
						skipped = true;
					}
					yield return null;
				}
				m_skipCanvas.SetActive(false);
			}
			else
			{
				while (!skipButton.JustPressed())
				{
					yield return null;
				}
				skipped = true;
			}
		}
	}

	private bool HasFinished()
	{
		return m_isFinished;
	}

	private void ChangeGameState(GameState state)
	{
		UserSystemUtils.ChangeGameState(state);
		m_state = state;
	}

	private bool AreAllUsersInGameState(GameState state)
	{
		return UserSystemUtils.AreAllUsersInGameState(ServerUserSystem.m_Users, state);
	}

	public void SkipToEnd()
	{
		m_isFinished = true;
	}
}
