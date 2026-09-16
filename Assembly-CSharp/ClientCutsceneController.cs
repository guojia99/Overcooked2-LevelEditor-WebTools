using System.Collections;
using System.Collections.Generic;
using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;
using UnityEngine.Playables;

public class ClientCutsceneController : ClientSynchroniserBase
{
	private CutsceneController m_controller;

	private List<Suppressor> m_controlsSuppressors = new List<Suppressor>();

	private List<PlayerControls> m_players = new List<PlayerControls>();

	private Canvas m_hudCanvas;

	private Canvas m_hoverIconCanvas;

	private GameObject m_skipCanvas;

	private bool m_skippable;

	private bool m_skipped;

	private const float c_skipHoldDuration = 1f;

	private const float c_skipPromptTimout = 2f;

	private bool m_uiEnabledPostPlayback = true;

	public override EntityType GetEntityType()
	{
		return EntityType.Cutscene;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_controller = (CutsceneController)synchronisedObject;
		Mailbox.Client.RegisterForMessageType(MessageType.DestroyChef, OnDestroyChefMessageReceived);
	}

	public override void ApplyServerEvent(Serialisable serialisable)
	{
		CutsceneStateMessage cutsceneStateMessage = (CutsceneStateMessage)serialisable;
		if (cutsceneStateMessage != null)
		{
			m_skipped = true;
		}
	}

	protected override void OnDestroy()
	{
		Mailbox.Client.UnregisterForMessageType(MessageType.DestroyChef, OnDestroyChefMessageReceived);
		base.OnDestroy();
	}

	public IEnumerator StartCutscene(CutsceneController.SetupData _setupData)
	{
		if (m_controller == null)
		{
			return null;
		}
		m_skippable = _setupData.skippable;
		m_uiEnabledPostPlayback = _setupData.postplaybackUIEnabled;
		GameObject[] array = GameObject.FindGameObjectsWithTag("Player");
		for (int i = 0; i < array.Length; i++)
		{
			m_players.Add(array[i].RequireComponent<PlayerControls>());
		}
		m_hudCanvas = GameUtils.GetNamedCanvas("ScalingHUDCanvas").RequireComponent<Canvas>();
		m_hoverIconCanvas = GameUtils.GetNamedCanvas("HoverIconCanvas").RequireComponent<Canvas>();
		if (m_controller.m_skipUIPrefab != null)
		{
			GameObject skipCanvas = GameUtils.InstantiateUIController(m_controller.m_skipUIPrefab, "UICanvas");
			m_skipCanvas = skipCanvas;
			m_skipCanvas.SetActive(false);
		}
		return RunCutscene(m_controller.m_director);
	}

	public void Shutdown()
	{
		if (m_skipCanvas != null)
		{
			Object.Destroy(m_skipCanvas.gameObject);
		}
	}

	public void OnDestroyChefMessageReceived(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
	{
		if (m_players == null)
		{
			return;
		}
		DestroyChefMessage destroyChefMessage = (DestroyChefMessage)message;
		EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(destroyChefMessage.m_Chef.m_Header.m_uEntityID);
		List<PlayerControls> list = new List<PlayerControls>();
		for (int i = 0; i < m_players.Count; i++)
		{
			if (null != m_players[i])
			{
				EntitySerialisationEntry entry2 = EntitySerialisationRegistry.GetEntry(m_players[i].gameObject);
				if (entry2 != null && entry2.m_Header.m_uEntityID != destroyChefMessage.m_Chef.m_Header.m_uEntityID)
				{
					list.Add(m_players[i]);
				}
			}
		}
		m_players = list;
	}

	private IEnumerator RunCutscene(PlayableDirector _director)
	{
		if (_director != null)
		{
			IEnumerator setupRoutine = RunPreCutsceneRoutine(_director);
			while (setupRoutine.MoveNext())
			{
				yield return null;
			}
			IEnumerator directorRoutine = RunCutsceneRoutine(_director);
			IEnumerator skipRoutine = RunSkipRoutine();
			while (directorRoutine.MoveNext())
			{
				if (ConnectionStatus.IsHost() || !ConnectionStatus.IsInSession())
				{
					if (!m_skipped && !skipRoutine.MoveNext())
					{
						m_controller.OnCutsceneSkipped();
						m_skipped = true;
					}
					yield return null;
				}
				else
				{
					yield return null;
				}
			}
			IEnumerator shutdownRoutine = RunPostCutsceneRoutine(_director);
			while (shutdownRoutine.MoveNext())
			{
				yield return null;
			}
		}
		if (m_skipCanvas != null)
		{
			Object.Destroy(m_skipCanvas.gameObject);
		}
	}

	private IEnumerator RunPreCutsceneRoutine(PlayableDirector _director)
	{
		_director.gameObject.SetActive(true);
		_director.Play();
		_director.Pause();
		yield return null;
		m_hudCanvas.enabled = false;
		m_hoverIconCanvas.enabled = false;
		if (m_controller.m_gameCamera != null)
		{
			m_controller.m_gameCamera.gameObject.SetActive(false);
		}
		m_controlsSuppressors.Clear();
		for (int i = 0; i < m_players.Count; i++)
		{
			m_controlsSuppressors.Add(m_players[i].Suppress(this));
		}
		for (int j = 0; j < m_players.Count; j++)
		{
			m_players[j].gameObject.SetActive(false);
		}
	}

	private IEnumerator RunCutsceneRoutine(PlayableDirector _director)
	{
		_director.Play();
		PlayableGraph graph = _director.playableGraph;
		while (!m_skipped && _director.time < _director.duration && graph.IsValid() && !graph.IsDone())
		{
			yield return null;
		}
		_director.Pause();
	}

	private IEnumerator RunPostCutsceneRoutine(PlayableDirector _director)
	{
		if (m_skipped)
		{
			yield return null;
		}
		_director.Stop();
		_director.gameObject.SetActive(false);
		if (m_uiEnabledPostPlayback)
		{
			m_hudCanvas.enabled = true;
			m_hoverIconCanvas.enabled = true;
		}
		if (m_controller.m_gameCamera != null)
		{
			m_controller.m_gameCamera.gameObject.SetActive(true);
		}
		for (int i = 0; i < m_controlsSuppressors.Count; i++)
		{
			m_controlsSuppressors[i].Release();
		}
		m_controlsSuppressors.Clear();
		for (int j = 0; j < m_players.Count; j++)
		{
			if (null != m_players[j])
			{
				m_players[j].gameObject.SetActive(true);
			}
		}
		if (m_skipped)
		{
			yield return null;
		}
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
		GateLogicalButton gatedSkipButton = new GateLogicalButton(rawSkipButton, () => !TimeManager.IsPaused(base.gameObject));
		TimedLogicalButton skipButton = new TimedLogicalButton(gatedSkipButton, TimedLogicalButton.Condition.HeldLonger, 1f);
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
}
