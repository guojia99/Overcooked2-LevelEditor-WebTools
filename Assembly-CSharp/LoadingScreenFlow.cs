using System;
using System.Collections.Generic;
using AssetBundles;
using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadingScreenFlow : MonoBehaviour
{
	public const string StartScene = "StartScreen";

	private static string s_nextScene = "StartScreen";

	private static GameState s_gameState;

	private static SceneLoaderHelper m_sceneLoaderHelper;

	private static AsyncOperation m_asyncOperation;

	private static IEnumerator<float> m_routine;

	private static bool m_loading;

	private static bool m_abortBackToStartScreen;

	private bool m_finished;

	private bool m_finishCalled;

	private GameSession m_gameSession;

	private static OnlineMultiplayerReturnCode<OnlineMultiplayerSessionDisconnectionResult> m_CachedDisconnectionError;

	private static OnlineMultiplayerReturnCode<OnlineMultiplayerConnectionModeErrorResult> m_CachedConnectionModeError;

	public static bool IsLoading
	{
		get
		{
			return m_loading;
		}
	}

	public static bool IsAbortingLoad
	{
		get
		{
			return m_abortBackToStartScreen;
		}
	}

	public string NextScene
	{
		get
		{
			return s_nextScene;
		}
	}

	public float Progress
	{
		get
		{
			if (m_routine != null)
			{
				return m_routine.Current;
			}
			if (m_sceneLoaderHelper != null)
			{
				return m_sceneLoaderHelper.GetProgress();
			}
			return -1f;
		}
	}

	public static bool IsLoadingStartScreen()
	{
		return s_nextScene == "StartScreen" || IsAbortingLoad;
	}

	private void Awake()
	{
		UnityEngine.Object.DontDestroyOnLoad(base.gameObject);
		m_CachedDisconnectionError = null;
		m_CachedConnectionModeError = null;
		Mailbox.Client.RegisterForMessageType(MessageType.GameState, OnGameStateChanged);
	}

	private void OnDestroy()
	{
		Mailbox.Client.UnregisterForMessageType(MessageType.GameState, OnGameStateChanged);
		DisconnectionHandler.SessionConnectionLostEvent = (GenericVoid)Delegate.Remove(DisconnectionHandler.SessionConnectionLostEvent, new GenericVoid(OnSessionConnectionLost));
		DisconnectionHandler.ConnectionModeErrorEvent = (GenericVoid<OnlineMultiplayerReturnCode<OnlineMultiplayerConnectionModeErrorResult>>)Delegate.Remove(DisconnectionHandler.ConnectionModeErrorEvent, new GenericVoid<OnlineMultiplayerReturnCode<OnlineMultiplayerConnectionModeErrorResult>>(OnConnectionModeError));
		DisconnectionHandler.KickedFromSessionEvent = (GenericVoid)Delegate.Remove(DisconnectionHandler.KickedFromSessionEvent, new GenericVoid(OnKickedFromSession));
		DisconnectionHandler.LocalDisconnectionEvent = (GenericVoid<OnlineMultiplayerReturnCode<OnlineMultiplayerSessionDisconnectionResult>>)Delegate.Remove(DisconnectionHandler.LocalDisconnectionEvent, new GenericVoid<OnlineMultiplayerReturnCode<OnlineMultiplayerSessionDisconnectionResult>>(OnLocalDisconnection));
		m_CachedDisconnectionError = null;
		m_CachedConnectionModeError = null;
	}

	public static void LoadScene(string _NextScene, GameState _GameState = GameState.NotSet)
	{
		if (!GameUtils.CanLoadScene(_NextScene))
		{
			return;
		}
		s_nextScene = _NextScene;
		s_gameState = _GameState;
		m_abortBackToStartScreen = false;
		ScreenTransitionManager transitionManager = GameUtils.RequireManager<ScreenTransitionManager>();
		transitionManager.StartTransitionUp(delegate
		{
			string sceneName = SceneManager.GetActiveScene().name;
			AssetBundleManager.LoadLevel("loading", "Loading", false);
			transitionManager.StartTransitionDown(delegate
			{
				StartLoading(sceneName);
			});
		});
		InviteMonitor.SwitchHandlerType(InviteMonitor.HandlerType.None);
		DisconnectionHandler.SessionConnectionLostEvent = (GenericVoid)Delegate.Combine(DisconnectionHandler.SessionConnectionLostEvent, new GenericVoid(OnSessionConnectionLost));
		DisconnectionHandler.ConnectionModeErrorEvent = (GenericVoid<OnlineMultiplayerReturnCode<OnlineMultiplayerConnectionModeErrorResult>>)Delegate.Combine(DisconnectionHandler.ConnectionModeErrorEvent, new GenericVoid<OnlineMultiplayerReturnCode<OnlineMultiplayerConnectionModeErrorResult>>(OnConnectionModeError));
		DisconnectionHandler.KickedFromSessionEvent = (GenericVoid)Delegate.Combine(DisconnectionHandler.KickedFromSessionEvent, new GenericVoid(OnKickedFromSession));
		DisconnectionHandler.LocalDisconnectionEvent = (GenericVoid<OnlineMultiplayerReturnCode<OnlineMultiplayerSessionDisconnectionResult>>)Delegate.Combine(DisconnectionHandler.LocalDisconnectionEvent, new GenericVoid<OnlineMultiplayerReturnCode<OnlineMultiplayerSessionDisconnectionResult>>(OnLocalDisconnection));
	}

	private static void OnSessionConnectionLost()
	{
		OnlineMultiplayerReturnCode<OnlineMultiplayerSessionDisconnectionResult> onlineMultiplayerReturnCode = new OnlineMultiplayerReturnCode<OnlineMultiplayerSessionDisconnectionResult>();
		onlineMultiplayerReturnCode.m_returnCode = OnlineMultiplayerSessionDisconnectionResult.eGeneric;
		m_CachedDisconnectionError = onlineMultiplayerReturnCode;
		RequestReturnToStartScreen();
	}

	private static void OnConnectionModeError(OnlineMultiplayerReturnCode<OnlineMultiplayerConnectionModeErrorResult> result)
	{
		m_CachedConnectionModeError = result;
		RequestReturnToStartScreen();
	}

	private static void OnKickedFromSession()
	{
		OnlineMultiplayerReturnCode<OnlineMultiplayerSessionDisconnectionResult> onlineMultiplayerReturnCode = new OnlineMultiplayerReturnCode<OnlineMultiplayerSessionDisconnectionResult>();
		onlineMultiplayerReturnCode.m_returnCode = OnlineMultiplayerSessionDisconnectionResult.eKicked;
		m_CachedDisconnectionError = onlineMultiplayerReturnCode;
		RequestReturnToStartScreen();
	}

	private static void OnLocalDisconnection(OnlineMultiplayerReturnCode<OnlineMultiplayerSessionDisconnectionResult> result)
	{
		m_CachedDisconnectionError = result;
		RequestReturnToStartScreen();
	}

	private void OnGameStateChanged(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
	{
		GameStateMessage gameStateMessage = (GameStateMessage)message;
		if (!m_finished && SceneManager.GetSceneByName(NextScene).isLoaded && gameStateMessage.m_State == s_gameState)
		{
			Finish();
		}
	}

	private void Start()
	{
		m_sceneLoaderHelper = base.gameObject.AddComponent<SceneLoaderHelper>();
	}

	private static void StartLoading(string activeScene = null)
	{
		if (m_sceneLoaderHelper != null)
		{
			if (s_nextScene == activeScene)
			{
				AssetBundleManager.UnloadAssetBundle(s_nextScene.ToLowerInvariant());
			}
			m_sceneLoaderHelper.LoadLevelAsync(s_nextScene, false);
			m_loading = true;
		}
	}

	private void Update()
	{
		if (m_sceneLoaderHelper != null)
		{
			m_sceneLoaderHelper.ActivateSceneWhenLoaded = !TimeManager.IsPaused(TimeManager.PauseLayer.System);
		}
		if (m_routine != null)
		{
			m_routine.MoveNext();
		}
		if (!m_finishCalled)
		{
			if (InviteMonitor.GetAcceptedInvite() != null && !NextScene.Equals("StartScreen") && ClientUserSystem.m_Users.Count == 1 && !m_abortBackToStartScreen)
			{
				RequestReturnToStartScreen();
			}
			else if (!m_finished && SceneManager.GetSceneByName(NextScene).isLoaded && (s_gameState == GameState.NotSet || m_abortBackToStartScreen))
			{
				Finish();
			}
		}
		if (m_finished)
		{
			UnityEngine.Object.Destroy(base.gameObject);
		}
	}

	private static void RequestReturnToStartScreen()
	{
		if (!m_abortBackToStartScreen)
		{
			m_abortBackToStartScreen = true;
			ConnectionModeSwitcher.RequestConnectionState(NetConnectionState.Offline, null, OnRequestConnectionStateOfflineComplete);
			MultiplayerController multiplayerController = GameUtils.RequireManager<MultiplayerController>();
			if (MultiplayerController.IsSynchronisationActive())
			{
				multiplayerController.StopSynchronisation();
			}
			if (s_nextScene != "StartScreen")
			{
				m_abortBackToStartScreen = true;
			}
		}
	}

	private static void OnRequestConnectionStateOfflineComplete(IConnectionModeSwitchStatus _status)
	{
		if (_status.GetResult() == eConnectionModeSwitchResult.Success)
		{
			ServerGameSetup.Mode = GameMode.OnlineKitchen;
		}
	}

	private static void LoadStartScreen()
	{
		s_gameState = GameState.NotSet;
		m_abortBackToStartScreen = false;
		if (s_nextScene != "StartScreen")
		{
			s_nextScene = "StartScreen";
			StartLoading();
		}
	}

	private void Finish()
	{
		if (m_abortBackToStartScreen)
		{
			LoadStartScreen();
			return;
		}
		m_finishCalled = true;
		PlayerManager playerManager = GameUtils.RequireManager<PlayerManager>();
		if (playerManager != null && playerManager.HasPlayer())
		{
			if (m_CachedDisconnectionError != null)
			{
				NetworkErrorDialog.ShowDialog(m_CachedDisconnectionError);
			}
			else if (m_CachedConnectionModeError != null)
			{
				NetworkErrorDialog.ShowDialog(m_CachedConnectionModeError);
			}
		}
		else if (m_CachedDisconnectionError != null || m_CachedConnectionModeError != null)
		{
			m_CachedDisconnectionError = null;
			m_CachedConnectionModeError = null;
		}
		ScreenTransitionManager transitionManager = GameUtils.RequireManager<ScreenTransitionManager>();
		transitionManager.StartTransitionUp(delegate
		{
			m_loading = false;
			m_finished = true;
			m_finishCalled = false;
			transitionManager.StartTransitionDown();
		});
	}
}
