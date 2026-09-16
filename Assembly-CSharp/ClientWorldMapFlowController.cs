using System;
using System.Collections;
using System.Collections.Generic;
using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ClientWorldMapFlowController : ClientSynchroniserBase
{
	private IEnumerator m_popupRoutine;

	private IEnumerator m_runIntro;

	private List<IEnumerator> m_unfoldRoutines = new List<IEnumerator>();

	private MapNode[] m_allNodes;

	private WorldMapFlowController m_baseObject;

	private NetworkErrorDialog m_NetworkErrorDialog = new NetworkErrorDialog();

	private List<WorldMapInfoPopup> m_popups = new List<WorldMapInfoPopup>();

	private Suppressor m_savingDisabledIcon;

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_allNodes = UnityEngine.Object.FindObjectsOfType<MapNode>();
		m_baseObject = (WorldMapFlowController)synchronisedObject;
		for (int i = 0; i < m_allNodes.Length; i++)
		{
			m_allNodes[i].StartUp();
		}
		RegisterPopups();
		if (ConnectionStatus.IsInSession() && !ConnectionStatus.IsHost())
		{
			GameSession gameSession = GameUtils.GetGameSession();
			if (!gameSession.Progress.UseSlaveSlot)
			{
				m_savingDisabledIcon = GameUtils.RequireManager<SpinnerIconManager>().Show(SpinnerIconManager.SpinnerIconType.SavingDisabled, this);
			}
		}
	}

	private IEnumerator StartIntroRoutine(MapNode[] unfoldedMapNodes)
	{
		yield return new WaitForSecondsRealtime(2f);
		m_runIntro = UnfoldNodesRoutine(unfoldedMapNodes, MapNodeUnfolded);
	}

	private void RegisterPopups()
	{
		if (m_baseObject.m_newGamePlusDialogPrefab != null)
		{
			NetworkUtils.RegisterSpawnablePrefab(base.gameObject, m_baseObject.m_newGamePlusDialogPrefab.gameObject, PopupSpawned);
		}
		if (m_baseObject.m_practiceModeDialogPrefab != null)
		{
			NetworkUtils.RegisterSpawnablePrefab(base.gameObject, m_baseObject.m_practiceModeDialogPrefab.gameObject, PopupSpawned);
		}
		if (m_baseObject.m_hordeModeDialogPrefab != null)
		{
			NetworkUtils.RegisterSpawnablePrefab(base.gameObject, m_baseObject.m_hordeModeDialogPrefab.gameObject, PopupSpawned);
		}
	}

	private void OnGameStateChanged(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
	{
		GameStateMessage gameStateMessage = (GameStateMessage)message;
		switch (gameStateMessage.m_State)
		{
		case GameState.RunMapUnfoldRoutine:
		{
			GameProgress progress = GameUtils.GetGameSession().Progress;
			int num = progress.SaveData.LastLevelEntered;
			if (num == -1)
			{
				num = progress.SaveData.FarthestProgressedLevel(false);
			}
			if (num != -1 && num < progress.GetSceneDirectory().Scenes.Length)
			{
				GameUtils.GetMetaGameProgress().SetLastPlayedTheme(progress.GetSceneDirectory().Scenes[num].Theme);
			}
			MapNode[] _array = new MapNode[0];
			PortalMapNode[] array = m_allNodes.ConvertAll((MapNode x) => x as PortalMapNode);
			array = array.AllRemoved_Predicate((PortalMapNode x) => x == null);
			for (int num2 = 0; num2 < array.Length; num2++)
			{
				if (m_baseObject.IsLevelUnlocked(array[num2]))
				{
					GameProgress.GameProgressData.LevelProgress progress2 = progress.GetProgress(array[num2].LevelIndex);
					if (!progress2.Completed && !progress2.Revealed && !DebugManager.Instance.GetOption("Unlock all levels") && !array[num2].ForceUnlocked)
					{
						ArrayUtils.PushBack(ref _array, array[num2]);
						continue;
					}
					array[num2].InstantUnfold();
					array[num2].SetAsStatic();
				}
				else
				{
					array[num2].SetAsStatic();
				}
			}
			SwitchMapNode[] array2 = m_allNodes.ConvertAll((MapNode x) => x as SwitchMapNode);
			array2 = array2.AllRemoved_Predicate((SwitchMapNode x) => x == null);
			for (int num3 = 0; num3 < array2.Length; num3++)
			{
				if (m_baseObject.IsSwitchSwitched(array2[num3]))
				{
					array2[num3].InstantUnfold();
				}
			}
			TeleportalMapNode[] array3 = m_allNodes.ConvertAll((MapNode x) => x as TeleportalMapNode);
			array3 = array3.AllRemoved_Predicate((TeleportalMapNode x) => x == null);
			Array.Sort(array3, (TeleportalMapNode x, TeleportalMapNode y) => x.World.CompareTo(y.World));
			foreach (TeleportalMapNode teleportalMapNode in array3)
			{
				if (teleportalMapNode.ShouldFocus())
				{
					ArrayUtils.PushBack(ref _array, teleportalMapNode);
					continue;
				}
				teleportalMapNode.InstantUnfold();
				teleportalMapNode.SetAsStatic();
			}
			StartCoroutine(StartIntroRoutine(_array));
			break;
		}
		case GameState.InMap:
			m_popupRoutine = ShowPopupsRoutine();
			break;
		}
	}

	protected void PopupSpawned(GameObject _spawned)
	{
		WorldMapInfoPopup item = _spawned.RequireComponent<WorldMapInfoPopup>();
		m_popups.Add(item);
		GameObject namedCanvas = GameUtils.GetNamedCanvas("ScalingHUDCanvas");
		if (namedCanvas != null)
		{
			_spawned.transform.SetParent(namedCanvas.transform);
			_spawned.transform.localPosition = Vector3.zero;
		}
		_spawned.SetActive(false);
	}

	private IEnumerator ShowPopupsRoutine()
	{
		if (m_popups.Count == 0)
		{
			yield break;
		}
		Camera mainCamera = Camera.main;
		WorldMapCamera worldMapCamera = mainCamera.gameObject.RequireComponent<WorldMapCamera>();
		MapAvatarControls avatarControls = worldMapCamera.AccessAvatar.RequireComponent<MapAvatarControls>();
		for (int i = 0; i < m_popups.Count; i++)
		{
			ClientWorldMapInfoPopup clientPopup = m_popups[i].gameObject.RequireComponent<ClientWorldMapInfoPopup>();
			if (clientPopup.CanShow())
			{
				worldMapCamera.enabled = false;
				avatarControls.enabled = false;
				IEnumerator popupRoutine = clientPopup.PopupRoutine();
				while (popupRoutine.MoveNext())
				{
					yield return null;
				}
			}
		}
		worldMapCamera.enabled = true;
		avatarControls.enabled = true;
	}

	private void Awake()
	{
		m_NetworkErrorDialog.Enable(OnNetworkDisconnectionConfirmed);
		Mailbox.Client.RegisterForMessageType(MessageType.GameState, OnGameStateChanged);
		InviteMonitor.SwitchHandlerType(InviteMonitor.HandlerType.Gameplay);
	}

	private void Start()
	{
		if (!LoadingScreenFlow.IsLoadingStartScreen())
		{
			GameUtils.LoadScene("InGameMenu", LoadSceneMode.Additive);
		}
		GameSession gameSession = GameUtils.GetGameSession();
		if (!ConnectionStatus.IsInSession() || ConnectionStatus.IsHost())
		{
			gameSession.SaveSession();
		}
		else if (gameSession.Progress.UseSlaveSlot)
		{
			gameSession.SaveSession();
		}
		else
		{
			GameUtils.RequireManager<SaveManager>().SaveMetaProgress();
		}
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		Mailbox.Client.UnregisterForMessageType(MessageType.GameState, OnGameStateChanged);
		m_NetworkErrorDialog.Disable();
		if (m_savingDisabledIcon != null)
		{
			m_savingDisabledIcon.Release();
			m_savingDisabledIcon = null;
		}
	}

	public override void UpdateSynchronising()
	{
		if (m_runIntro != null && !m_runIntro.MoveNext())
		{
			ClientMessenger.GameState(GameState.RanMapUnfoldRoutine);
			m_runIntro = null;
		}
		m_unfoldRoutines.RemoveAll((IEnumerator x) => !x.MoveNext());
		if (m_popupRoutine != null && !m_popupRoutine.MoveNext())
		{
			m_popupRoutine = null;
		}
	}

	private void LateUpdate()
	{
		if (m_runIntro != null || m_allNodes == null || m_unfoldRoutines.Count != 0)
		{
			return;
		}
		for (int i = 0; i < m_allNodes.Length; i++)
		{
			if (!m_allNodes[i].IsStatic() && m_allNodes[i].IsIdle())
			{
				m_allNodes[i].SetAsStatic();
			}
		}
	}

	private void MapNodeUnfolded(MapNode _node)
	{
		PortalMapNode portalMapNode = _node as PortalMapNode;
		if (portalMapNode != null)
		{
			GameSession gameSession = GameUtils.GetGameSession();
			if (gameSession.Progress != null)
			{
				GameProgress.GameProgressData.LevelProgress progress = gameSession.Progress.GetProgress(portalMapNode.LevelIndex);
				progress.Revealed = true;
			}
			return;
		}
		TeleportalMapNode teleportalMapNode = _node as TeleportalMapNode;
		if (teleportalMapNode != null)
		{
			GameSession gameSession2 = GameUtils.GetGameSession();
			if (gameSession2.Progress != null)
			{
				gameSession2.Progress.RecordTeleportalRevealed(teleportalMapNode.World);
			}
		}
	}

	public void UnfoldSwitchMapNode(SwitchMapNode _switch)
	{
		IEnumerator routine = UnfoldNodesRoutine(new MapNode[1] { _switch });
		StartCoroutine(routine);
	}

	private void CalculateTransitionData(float _distance, out float _gradLimit, out float _timeToMax)
	{
		float accelerationTime = m_baseObject.m_unfoldSequenceData.AccelerationTime;
		float idealTransitTime = m_baseObject.m_unfoldSequenceData.IdealTransitTime;
		float num = Mathf.Min(idealTransitTime / 2f, accelerationTime);
		float num2 = (float)Math.PI;
		float num3 = idealTransitTime - num - accelerationTime * Mathf.Sin(num2 * num / accelerationTime) / num2;
		float value = _distance / num3;
		_gradLimit = Mathf.Clamp(value, m_baseObject.m_unfoldSequenceData.MinMoveSpeed, m_baseObject.m_unfoldSequenceData.MaxMoveSpeed);
		_timeToMax = accelerationTime;
	}

	private IEnumerator BlendCameraToPosition(Vector3 _position)
	{
		Camera mainCamera = Camera.main;
		float distanceToIdeal = (_position - mainCamera.transform.position).magnitude;
		float currentGradient = 0f;
		float gradLimit;
		float accelTime;
		CalculateTransitionData(distanceToIdeal, out gradLimit, out accelTime);
		while (true)
		{
			distanceToIdeal = (_position - mainCamera.transform.position).magnitude;
			MathUtils.AdvanceToTarget_Sinusoidal(_nDeltaTime: TimeManager.GetDeltaTime(base.gameObject), _nCurrentX: ref distanceToIdeal, _nCurrentGradient: ref currentGradient, _nTargetX: 0f, _nGradientLimit: gradLimit, _nTimeToMax: accelTime);
			Vector3 pos = _position - (_position - mainCamera.transform.position).SafeNormalised(Vector3.zero) * distanceToIdeal;
			mainCamera.transform.position = pos;
			if (distanceToIdeal < 0.1f)
			{
				break;
			}
			yield return null;
		}
	}

	private IEnumerator UnfoldNodesRoutine(MapNode[] _mapNodes, VoidGeneric<MapNode> _onShown = null)
	{
		if (_mapNodes.Length == 0)
		{
			yield break;
		}
		Camera mainCamera = Camera.main;
		WorldMapCamera worldMapCamera = mainCamera.gameObject.RequireComponent<WorldMapCamera>();
		worldMapCamera.enabled = false;
		MapAvatarControls avatarControls = worldMapCamera.AccessAvatar.RequireComponent<MapAvatarControls>();
		avatarControls.enabled = false;
		Vector3 idealOffset = worldMapCamera.AccessIdealOffset;
		foreach (MapNode node in _mapNodes)
		{
			IEnumerator blendTo = BlendCameraToPosition(node.transform.position + idealOffset);
			while (blendTo.MoveNext())
			{
				yield return null;
			}
			IEnumerator unfoldRoutine = node.UnfoldFlow();
			m_unfoldRoutines.Add(unfoldRoutine);
			IEnumerator wait = CoroutineUtils.TimerRoutine(m_baseObject.m_unfoldSequenceData.TimePerNode, base.gameObject.layer);
			while (wait.MoveNext())
			{
				yield return null;
			}
		}
		foreach (MapNode node2 in _mapNodes)
		{
			List<ClientWorldMapInfoPopup.InfoPopupShowRequest> popups = node2.GetPopups();
			for (int k = 0; k < popups.Count; k++)
			{
				ClientWorldMapInfoPopup.InfoPopupShowRequest request = popups[k];
				if (request.m_popup.CanShow())
				{
					IEnumerator blendToPopup = BlendCameraToPosition(request.m_requester.position + idealOffset);
					while (blendToPopup.MoveNext())
					{
						yield return null;
					}
					IEnumerator popupRoutine = request.m_popup.PopupRoutine();
					while (popupRoutine.MoveNext())
					{
						yield return null;
					}
				}
			}
		}
		foreach (MapNode param in _mapNodes)
		{
			if (_onShown != null)
			{
				_onShown(param);
			}
		}
		IEnumerator blendBack = BlendCameraToPosition(worldMapCamera.GetIdealLocation());
		while (blendBack.MoveNext())
		{
			yield return null;
		}
		worldMapCamera.enabled = true;
		avatarControls.enabled = true;
	}

	private void OnNetworkDisconnectionConfirmed()
	{
		ServerGameSetup.Mode = GameMode.OnlineKitchen;
		LoadingScreenFlow.LoadScene("StartScreen");
	}
}
