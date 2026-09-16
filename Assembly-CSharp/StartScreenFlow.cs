using System;
using System.Collections;
using System.Collections.Generic;
using AssetBundles;
using Team17.Online;
using UnityEngine;
using UnityEngine.PostProcessing;

[AddComponentMenu("Scripts/Game/Flow/FrontEndFlow")]
public class StartScreenFlow : MonoBehaviour
{
	[Serializable]
	public class ThemeBackgroundCollection
	{
		[SerializeField]
		public SceneDirectoryData.LevelTheme[] Themes = new SceneDirectoryData.LevelTheme[0];

		[SerializeField]
		public StartScreenBackgroundData[] Backgrounds = new StartScreenBackgroundData[0];
	}

	[Serializable]
	private class DLCSerializedBackgroundData : DLCSerializedData<ThemeBackgroundCollection>
	{
	}

	private static StartScreenFlow s_Instance;

	[SerializeField]
	private T17FrontendFlow m_FrontendFlow;

	[SerializeField]
	private GameObject m_PressACanvasObject;

	[SerializeField]
	private GameObject m_frontend;

	[SerializeField]
	private GameObject m_startScreen;

	[SerializeField]
	private SafeAreaAdjusterMenu m_safeAreaAdjustmentScreen;

	[SerializeField]
	private Canvas m_PopupCanvas;

	[SerializeField]
	private PopupDataScriptableObject m_PopupData;

	[SerializeField]
	private NewContentPopup m_newContentPopup;

	[Header("Art")]
	[SerializeField]
	private Animator m_shutterAnim;

	[SerializeField]
	private Camera m_Camera;

	[SerializeField]
	private string m_DefaultBackgroundScene;

	[Space]
	[SerializeField]
	private PersistentMusic m_MusicSource;

	[SerializeField]
	private AudioSource m_AmbienceSource;

	[Space]
	[SerializeField]
	private DLCSerializedBackgroundData m_Backgrounds = new DLCSerializedBackgroundData();

	private ISaveManager m_SaveManager;

	private PlayerManager m_PlayerManager;

	private DLCManager m_dlcManager;

	private Dictionary<int, StartScreenBackgroundData> m_ExtraBackgrounds = new Dictionary<int, StartScreenBackgroundData>();

	private SerializedSceneData.CameraData m_DefaultCameraData = new SerializedSceneData.CameraData();

	private SerializedSceneData.RenderData m_DefaultRenderData = new SerializedSceneData.RenderData();

	private SerializedSceneData.AudioData m_DefaultAudioData = new SerializedSceneData.AudioData();

	private Queue<PopupData> m_PopupQueue = new Queue<PopupData>();

	private GameObject m_currentPopup;

	private static bool m_bCheckingForEngagement = true;

	private string m_CurrentBackgroundScene;

	public static StartScreenFlow Instance
	{
		get
		{
			return s_Instance;
		}
	}

	private void Awake()
	{
		if (s_Instance != null)
		{
			UnityEngine.Object.Destroy(this);
		}
		else
		{
			s_Instance = this;
		}
		for (int i = 0; i < m_PopupData.m_Popups.Count; i++)
		{
			if (m_PopupData.m_Popups[i].IsAvailableOnCurrentPlatform())
			{
				m_PopupQueue.Enqueue(m_PopupData.m_Popups[i]);
			}
		}
		NewContentPopup newContentPopup = m_newContentPopup;
		newContentPopup.OnHide = (BaseMenuBehaviour.BaseMenuBehaviourEvent)Delegate.Combine(newContentPopup.OnHide, new BaseMenuBehaviour.BaseMenuBehaviourEvent(OnNewContentPopupHide));
	}

	private void Start()
	{
		m_SaveManager = GameUtils.RequireManagerInterface<ISaveManager>();
		m_PlayerManager = GameUtils.RequireManager<PlayerManager>();
		m_dlcManager = GameUtils.RequireManager<DLCManager>();
		SetupFrontendBackgrounds();
		if (!ConnectionStatus.IsInSession() || ConnectionStatus.IsHost())
		{
			ServerGameSetup.Mode = GameMode.OnlineKitchen;
		}
		ServerUserSystem.UnlockEngagement();
		if ((m_PlayerManager.HasPlayer() || m_PlayerManager.IsEngagingSlot(EngagementSlot.One)) && m_SaveManager.GetMetaGameProgress() != null)
		{
			m_bCheckingForEngagement = false;
			m_startScreen.SetActive(false);
			m_frontend.SetActive(true);
			m_PressACanvasObject.SetActive(false);
			MetaGameProgress metaGameProgress = m_SaveManager.GetMetaGameProgress();
			if (!SwapFrontendBackground(metaGameProgress.GetLastPlayedTheme()))
			{
			}
			m_shutterAnim.SetBool("ForceOpen", true);
			m_shutterAnim.SetBool("isOpen", true);
			m_FrontendFlow.PullBackCamera();
		}
		else
		{
			SwapFrontendBackground(SceneDirectoryData.LevelTheme.Null);
			ResetToJustStarted(false);
		}
		RichPresenceManager.SetGameMode(GameMode.OnlineKitchen);
		DisconnectionHandler.KickedFromSessionEvent = (GenericVoid)Delegate.Combine(DisconnectionHandler.KickedFromSessionEvent, new GenericVoid(OnSessionKicked));
	}

	protected void ResetToJustStarted(bool resetCamera = true)
	{
		T17EventSystemsManager instance = T17EventSystemsManager.Instance;
		m_bCheckingForEngagement = true;
		m_startScreen.SetActive(true);
		m_frontend.SetActive(false);
		m_PressACanvasObject.SetActive(true);
		m_shutterAnim.SetBool("ForceOpen", false);
		m_shutterAnim.SetBool("isOpen", false);
		if (resetCamera)
		{
			m_FrontendFlow.ResetCamera();
		}
		if (m_FrontendFlow != null)
		{
			m_FrontendFlow.BlockFocusKitchen = false;
		}
		GameObject gameObject = GameObject.Find("EventSystems");
		if (gameObject != null)
		{
			gameObject.SetActive(true);
		}
		m_SaveManager.UnloadProfile();
		AchievementManager achievementManager = GameUtils.RequireManager<AchievementManager>();
		if (achievementManager != null)
		{
			achievementManager.Unload();
		}
		m_PlayerManager.DisengagePad(EngagementSlot.One);
		if (instance != null)
		{
			instance.ResetAll();
		}
	}

	private void Update()
	{
		if (m_bCheckingForEngagement)
		{
			CheckForEngagement();
		}
		else if (m_PressACanvasObject != null)
		{
			m_PressACanvasObject.SetActive(false);
		}
	}

	private void CheckForEngagement()
	{
		EngagmentCircumstances o_engagment;
		ControlPadInput.PadNum engagementPad = m_PlayerManager.GetEngagementPad(out o_engagment);
		if (engagementPad != ControlPadInput.PadNum.Count)
		{
			m_PlayerManager.StartGameownerEngagement(engagementPad, o_engagment, OnEngagementFinished);
			m_bCheckingForEngagement = false;
		}
	}

	private void OnEngagementFinished(GamepadUser _param1)
	{
		if (!m_PlayerManager.HasPlayer() || !m_PlayerManager.HasSavablePlayer())
		{
		}
		if (m_PlayerManager.HasPlayer())
		{
			GameUtils.TriggerAudio(GameOneShotAudioTag.UIPressStart, base.gameObject.layer);
			StartCoroutine(StartLoadProfile(OnLoadProfileComplete));
		}
		else
		{
			ResetToJustStarted();
		}
	}

	private IEnumerator StartLoadProfile(CallbackVoid onComplete)
	{
		GamepadUser user = m_PlayerManager.GetUser(EngagementSlot.One);
		if (T17EventSystemsManager.Instance.GetEventSystemForGamepadUser(user) == null)
		{
			T17EventSystemsManager.Instance.AssignFreeEventSystemToGamepadUser(user);
		}
		m_SaveManager.DestroyMetaSession();
		IEnumerator<SaveLoadResult?> routine = m_SaveManager.LoadProfile(user);
		while (routine.MoveNext())
		{
			yield return null;
		}
		SaveLoadResult? current = routine.Current;
		if (current.GetValueOrDefault() == SaveLoadResult.Exists && current.HasValue)
		{
			if (onComplete != null)
			{
				onComplete();
			}
		}
		else if (routine.Current == SaveLoadResult.NotExist || routine.Current == SaveLoadResult.NoSpace)
		{
			m_SaveManager.DestroyMetaSession();
			m_SaveManager.CreateMetaSession();
			m_SaveManager.SaveMetaProgress(delegate(SaveSystemStatus _status)
			{
				if (_status.Status == SaveSystemStatus.SaveStatus.Complete)
				{
					if (_status.Result != SaveLoadResult.Cancel)
					{
						StartCoroutine(StartLoadProfile(onComplete));
					}
					else
					{
						ResetToJustStarted();
					}
				}
			});
		}
		else if (routine.Current == SaveLoadResult.Cancel)
		{
			ResetToJustStarted();
		}
	}

	private void OnLoadProfileComplete()
	{
		DisconnectionHandler.KickedFromSessionEvent = (GenericVoid)Delegate.Remove(DisconnectionHandler.KickedFromSessionEvent, new GenericVoid(OnSessionKicked));
		if (m_PlayerManager.HasPlayer())
		{
			GamepadUser user = m_PlayerManager.GetUser(EngagementSlot.One);
			IOption option = GameUtils.GetMetaGameProgress().GetOption(OptionsData.OptionType.HasSetSafeArea);
			if (option.GetOption() == 0 && m_safeAreaAdjustmentScreen != null)
			{
				if (!m_safeAreaAdjustmentScreen.gameObject.activeSelf)
				{
					m_safeAreaAdjustmentScreen.Show(user, null, base.gameObject);
					SafeAreaAdjusterMenu safeAreaAdjustmentScreen = m_safeAreaAdjustmentScreen;
					safeAreaAdjustmentScreen.OnHide = (BaseMenuBehaviour.BaseMenuBehaviourEvent)Delegate.Combine(safeAreaAdjustmentScreen.OnHide, (BaseMenuBehaviour.BaseMenuBehaviourEvent)delegate
					{
						GameUtils.TriggerAudio(GameOneShotAudioTag.UIConfirmScreenSize, base.gameObject.layer);
						m_frontend.SetActive(true);
						m_startScreen.SetActive(false);
						m_shutterAnim.SetBool("isOpen", true);
						m_FrontendFlow.PullBackCamera();
						StartCoroutine(ShowOnStartPopups());
					});
				}
			}
			else
			{
				m_frontend.SetActive(true);
				m_startScreen.SetActive(false);
				m_shutterAnim.SetBool("isOpen", true);
				m_FrontendFlow.PullBackCamera();
				StartCoroutine(ShowOnStartPopups());
			}
		}
		else
		{
			ResetToJustStarted();
		}
	}

	protected virtual void OnSessionKicked()
	{
	}

	public bool CanCancel()
	{
		return true;
	}

	public void OnCancel()
	{
		if (CanCancel())
		{
			GameUtils.TriggerAudio(GameOneShotAudioTag.StartScreenBack, base.gameObject.layer);
			throw new Exception("OnCancel requires implementation - move back?");
		}
	}

	public virtual void OnDestroy()
	{
		if (s_Instance == this)
		{
			m_bCheckingForEngagement = true;
		}
		DisconnectionHandler.KickedFromSessionEvent = (GenericVoid)Delegate.Remove(DisconnectionHandler.KickedFromSessionEvent, new GenericVoid(OnSessionKicked));
		NewContentPopup newContentPopup = m_newContentPopup;
		newContentPopup.OnHide = (BaseMenuBehaviour.BaseMenuBehaviourEvent)Delegate.Remove(newContentPopup.OnHide, new BaseMenuBehaviour.BaseMenuBehaviourEvent(OnNewContentPopupHide));
	}

	private void SetupFrontendBackgrounds()
	{
		m_ExtraBackgrounds.Clear();
		ThemeBackgroundCollection[] allData = m_Backgrounds.AllData;
		for (int i = 0; i < allData.Length; i++)
		{
			if (allData[i] == null)
			{
				continue;
			}
			SceneDirectoryData.LevelTheme[] themes = allData[i].Themes;
			StartScreenBackgroundData[] backgrounds = allData[i].Backgrounds;
			if (themes == null || backgrounds == null)
			{
				continue;
			}
			int num = Mathf.Min(themes.Length, backgrounds.Length);
			for (int j = 0; j < num; j++)
			{
				if (backgrounds[j] != null)
				{
					m_ExtraBackgrounds.Add((int)themes[j], backgrounds[j]);
				}
			}
		}
		if (m_Camera != null && m_Camera.gameObject != null)
		{
			PostProcessingBehaviour postProcessingBehaviour = m_Camera.gameObject.RequestComponent<PostProcessingBehaviour>();
			if (postProcessingBehaviour != null)
			{
				m_DefaultCameraData.PostProcessing = postProcessingBehaviour.profile;
			}
		}
		m_DefaultRenderData.SkyboxMaterial = RenderSettings.skybox;
		m_DefaultRenderData.AmbientSource = RenderSettings.ambientMode;
		m_DefaultRenderData.SkyboxData.SkyboxColour = RenderSettings.ambientSkyColor;
		m_DefaultRenderData.GradientData.SkyColour = RenderSettings.ambientSkyColor;
		m_DefaultRenderData.GradientData.EquatorColour = RenderSettings.ambientEquatorColor;
		m_DefaultRenderData.GradientData.GroundColour = RenderSettings.ambientGroundColor;
		m_DefaultRenderData.ColorData.Colour = RenderSettings.ambientSkyColor;
		m_DefaultRenderData.ReflectionSource = RenderSettings.defaultReflectionMode;
		m_DefaultRenderData.SkyboxReflectionData.Resolution = RenderSettings.defaultReflectionResolution;
		m_DefaultRenderData.SkyboxReflectionData.IntensityMultiplier = RenderSettings.reflectionIntensity;
		m_DefaultRenderData.SkyboxReflectionData.Bounces = RenderSettings.reflectionBounces;
		m_DefaultRenderData.CustomReflectionData.Cubemap = RenderSettings.customReflection;
		m_DefaultRenderData.CustomReflectionData.IntensityMultiplier = RenderSettings.reflectionIntensity;
		m_DefaultRenderData.CustomReflectionData.Bounces = RenderSettings.reflectionBounces;
		if (m_MusicSource != null && m_MusicSource.gameObject != null)
		{
			AudioSource audioSource = m_MusicSource.gameObject.RequestComponent<AudioSource>();
			if (audioSource != null)
			{
				m_DefaultAudioData.Music = audioSource.clip;
			}
		}
		if (m_AmbienceSource != null && m_AmbienceSource.gameObject != null)
		{
			m_DefaultAudioData.Ambience = m_AmbienceSource.clip;
		}
	}

	private bool SwapFrontendBackground(SceneDirectoryData.LevelTheme _theme)
	{
		if (_theme == SceneDirectoryData.LevelTheme.Null)
		{
			return LoadDefaultBackgroundArt();
		}
		StartScreenBackgroundData value = null;
		if (m_ExtraBackgrounds.TryGetValue((int)_theme, out value))
		{
			bool flag = LoadBackgroundArt(value.BackgroundScene);
			flag |= StartScreenBackgroundDataUtils.SwapBackgroundAudio(value.AudioSettings, ref m_MusicSource, ref m_AmbienceSource, true);
			flag |= SwapCameraSettings(value.CameraSettings);
			flag |= StartScreenBackgroundDataUtils.SetRenderData(value.SceneSettings);
			return flag | SwapChefHats(value.ChefHat);
		}
		return LoadDefaultBackgroundArt();
	}

	private bool LoadDefaultBackgroundArt()
	{
		bool flag = LoadBackgroundArt(m_DefaultBackgroundScene);
		flag |= StartScreenBackgroundDataUtils.SwapBackgroundAudio(m_DefaultAudioData, ref m_MusicSource, ref m_AmbienceSource, true);
		flag |= SwapCameraSettings(m_DefaultCameraData);
		flag |= StartScreenBackgroundDataUtils.SetRenderData(m_DefaultRenderData);
		return flag | SwapChefHats(HatMeshVisibility.VisState.Fancy);
	}

	private bool LoadBackgroundArt(string _backgroundScene)
	{
		string assetBundleName = _backgroundScene.ToLower();
		AssetBundleManager.LoadLevel(assetBundleName, _backgroundScene, true);
		return true;
	}

	private bool SwapCameraSettings(SerializedSceneData.CameraData _settings)
	{
		if (m_Camera != null && m_Camera.gameObject != null)
		{
			PostProcessingBehaviour postProcessingBehaviour = m_Camera.gameObject.RequestComponent<PostProcessingBehaviour>();
			if (postProcessingBehaviour != null)
			{
				postProcessingBehaviour.profile = _settings.PostProcessing;
			}
			FogConfig fogConfig = m_Camera.gameObject.RequestComponent<FogConfig>();
			if (fogConfig != null)
			{
				fogConfig.m_fogKind = _settings.Fog.m_kind;
				fogConfig.m_fogOffset = _settings.Fog.m_fogOffset;
				fogConfig.m_fogNear = _settings.Fog.m_fogNear;
				fogConfig.m_fogFar = _settings.Fog.m_fogFar;
				fogConfig.m_fogColour = _settings.Fog.m_fogColour;
				fogConfig.ForceUpdate();
			}
			return true;
		}
		return false;
	}

	private bool SwapChefHats(HatMeshVisibility.VisState _hat)
	{
		FrontendPlayerLobby[] componentsInChildren = m_FrontendFlow.GetComponentsInChildren<FrontendPlayerLobby>(true);
		if (componentsInChildren.Length > 0)
		{
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				componentsInChildren[i].m_ChefHat = _hat;
			}
			return true;
		}
		return false;
	}

	private IEnumerator ShowOnStartPopups()
	{
		T17EventSystem eventSys = T17EventSystemsManager.Instance.GetEventSystemForEngagementSlot(EngagementSlot.One);
		Suppressor suppressor = null;
		if (eventSys != null)
		{
			suppressor = eventSys.Disable(this);
		}
		yield return null;
		while (T17FrontendFlow.Instance.IsCameraTransitioning())
		{
			yield return null;
		}
		yield return null;
		T17FrontendFlow.Instance.BlockFocusKitchen = true;
		if (suppressor != null)
		{
			suppressor.Release();
		}
		GlobalSave saveGame = GameUtils.GetMetaGameProgress().SaveData;
		long utcNowTicks = DateTime.UtcNow.Ticks;
		while (m_PopupQueue.Count != 0)
		{
			if (m_currentPopup == null)
			{
				yield return null;
				PopupData popupData = m_PopupQueue.Dequeue();
				bool hasSeen = false;
				saveGame.Get(popupData.m_saveGameString, out hasSeen, false);
				if (!hasSeen && utcNowTicks < popupData.m_disableTicks)
				{
					switch (popupData.m_kind)
					{
					case PopupData.Kind.SwitchKitchenTutorial:
						m_currentPopup = UnityEngine.Object.Instantiate(popupData.m_prefab, m_PopupCanvas.transform);
						break;
					case PopupData.Kind.DLC:
					case PopupData.Kind.Update:
						if (T17FrontendFlow.Instance != null)
						{
							m_currentPopup = m_newContentPopup.gameObject;
							m_newContentPopup.m_popupData = popupData;
							T17FrontendFlow.Instance.m_Rootmenu.OpenFrontendMenu(m_newContentPopup);
						}
						break;
					}
					saveGame.Set(popupData.m_saveGameString, true);
				}
			}
			yield return null;
		}
		if (m_SaveManager != null)
		{
			m_SaveManager.RegisterOnIdle(delegate
			{
				m_SaveManager.SaveMetaProgress();
			});
		}
		while (m_currentPopup != null)
		{
			yield return null;
		}
		T17FrontendFlow.Instance.BlockFocusKitchen = false;
	}

	private void OnNewContentPopupHide(BaseMenuBehaviour menu)
	{
		m_currentPopup = null;
	}
}
