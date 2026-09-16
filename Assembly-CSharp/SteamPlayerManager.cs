using System;
using System.Text;
using InControl;
using Steamworks;
using Team17.Online;
using UnityEngine;

public class SteamPlayerManager : PCPlayerManager
{
	public class SteamPlayerProfile : PCPlayerProfile
	{
		public CSteamID m_steamID;

		private string m_UID = string.Empty;

		private string m_username = string.Empty;

		public override string UID
		{
			get
			{
				return m_UID;
			}
		}

		public override string DisplayName
		{
			get
			{
				return m_username;
			}
		}

		public SteamPlayerProfile(CSteamID _steamID, string _username, PlayerActionSet _input)
			: base(_input)
		{
			m_steamID = _steamID;
			m_UID = m_steamID.m_SteamID + ((_input.Device == null) ? "Keyboard" : _input.Device.Meta);
			m_username = _username;
		}
	}

	private static readonly AppId_t c_appID = new AppId_t(728880u);

	private static SteamPlayerManager s_instance;

	protected Callback<GameOverlayActivated_t> m_gameOverlayActivatedCallback;

	protected Callback<DurationControl_t> m_durationControlCallback;

	private EDurationControlProgress m_durationControlProgress;

	private bool m_bApplicable;

	private static bool s_EverInialized;

	private bool m_bInitialized;

	private bool m_applicationQuitting;

	public static GenericVoid<bool> OverlayVisbilityChanged = delegate
	{
	};

	private SteamAPIWarningMessageHook_t m_SteamAPIWarningMessageHook;

	public static bool Initialized
	{
		get
		{
			return s_instance != null && s_instance.m_bInitialized;
		}
	}

	public AppId_t SteamAppID
	{
		get
		{
			return c_appID;
		}
	}

	private static void SteamAPIDebugTextHook(int nSeverity, StringBuilder pchDebugText)
	{
	}

	protected override void Awake()
	{
		base.Awake();
		if (s_instance != null)
		{
			UnityEngine.Object.Destroy(base.gameObject);
			return;
		}
		s_instance = this;
		if (s_EverInialized)
		{
			throw new Exception("Tried to Initialize the SteamAPI twice in one session!");
		}
		UnityEngine.Object.DontDestroyOnLoad(base.gameObject);
		if (!Packsize.Test())
		{
		}
		if (!DllCheck.Test())
		{
		}
		try
		{
			if (SteamAPI.RestartAppIfNecessary(c_appID))
			{
				Application.Quit();
				return;
			}
		}
		catch (DllNotFoundException)
		{
			Application.Quit();
			return;
		}
		m_bInitialized = SteamAPI.Init();
		if (m_bInitialized)
		{
			m_durationControlCallback = Callback<DurationControl_t>.Create(OnDurationControl);
			s_EverInialized = true;
		}
	}

	protected override PCPlayerProfile EngagePadToSlot(ControlPadInput.PadNum _engagingPadNum, EngagementSlot _intendedSlot)
	{
		PlayerActionSet input = PCPadInputProvider.EngagePad(_engagingPadNum, (ControlPadInput.PadNum)_intendedSlot);
		SteamPlayerProfile steamPlayerProfile = new SteamPlayerProfile(SteamUser.GetSteamID(), SteamFriends.GetPersonaName(), input);
		if (m_lostUsers.Count > 0 && m_lostUsers[0].UID != steamPlayerProfile.UID)
		{
			PCPadInputProvider.DisengagePad((ControlPadInput.PadNum)_intendedSlot);
			return null;
		}
		steamPlayerProfile.StickyEngagement = _intendedSlot == EngagementSlot.One;
		AssignProfileToSlot(_intendedSlot, steamPlayerProfile);
		return steamPlayerProfile;
	}

	private void OnEnable()
	{
		if (s_instance == null)
		{
			s_instance = this;
		}
		if (m_bInitialized && m_SteamAPIWarningMessageHook == null)
		{
			m_SteamAPIWarningMessageHook = SteamAPIDebugTextHook;
			SteamClient.SetWarningMessageHook(m_SteamAPIWarningMessageHook);
		}
	}

	private new void OnDestroy()
	{
		if (!(s_instance != this))
		{
			s_instance = null;
			if (m_bInitialized)
			{
				SteamAPI.Shutdown();
			}
		}
	}

	private void OnApplicationQuit()
	{
		m_applicationQuitting = true;
	}

	protected override void Update()
	{
		base.Update();
		if (m_bInitialized && !m_applicationQuitting)
		{
			SteamAPI.RunCallbacks();
		}
	}

	public override void ShowGamerCard(GamepadUser localUser)
	{
		CSteamID steamID = SteamUser.GetSteamID();
		SteamFriends.ActivateGameOverlayToUser("steamid", steamID);
	}

	public override void ShowGamerCard(OnlineUserPlatformId onlineUser)
	{
		if (onlineUser != null)
		{
			SteamFriends.ActivateGameOverlayToUser("steamid", onlineUser.m_steamId);
		}
	}

	public override bool SupportsInvitesForAnyUser()
	{
		return true;
	}

	private void OnGameOverlayActivated(GameOverlayActivated_t param)
	{
		if (T17EventSystemsManager.Instance != null)
		{
			bool flag = Convert.ToBoolean(param.m_bActive);
			if (flag)
			{
				T17EventSystemsManager.Instance.DisableAllEventSystemsExceptFor(null);
			}
			else
			{
				T17EventSystemsManager.Instance.EnableAllEventSystems();
			}
			OverlayVisbilityChanged(flag);
		}
	}

	private void OnDurationControl(DurationControl_t param)
	{
		m_durationControlProgress = param.m_progress;
		m_bApplicable = param.m_bApplicable;
	}

	public static EDurationControlProgress GetDurationControlProgress()
	{
		return s_instance.m_durationControlProgress;
	}

	public static float GetDurationControlModifier()
	{
		switch (s_instance.m_durationControlProgress)
		{
		case EDurationControlProgress.k_EDurationControlProgress_Full:
			return 1f;
		case EDurationControlProgress.k_EDurationControlProgress_Half:
			return 0.5f;
		case EDurationControlProgress.k_EDurationControlProgress_None:
			return 0f;
		default:
			return 1f;
		}
	}

	public static bool IsProgressModifierApplicable()
	{
		return SteamUtils.IsSteamChinaLauncher() && s_instance.m_bApplicable;
	}
}
