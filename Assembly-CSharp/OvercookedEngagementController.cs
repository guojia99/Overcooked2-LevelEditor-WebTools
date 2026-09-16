using System.Collections.Generic;
using Team17.Online;
using UnityEngine;
using UnityEngine.SceneManagement;

[ExecutionDependency(typeof(PlayerManager))]
public class OvercookedEngagementController : Manager
{
	public enum LevelType
	{
		WithChefs = 0,
		WithoutChefs = 1
	}

	[SerializeField]
	[AssignComponent(Editorbility.NonEditable)]
	private PlayerManager m_playerManager;

	private LevelType m_levelType;

	private bool m_isClientMode;

	public LevelType GlobalLevelType
	{
		get
		{
			return m_levelType;
		}
	}

	public bool IsClientMode
	{
		get
		{
			return m_isClientMode;
		}
		set
		{
			m_isClientMode = value;
			Refresh();
		}
	}

	private void Awake()
	{
		Refresh();
		m_playerManager.EngagementChangeCallback += OnEngagementChanged;
		LobbyUIController.OpenCloseCallback += OnLobbyOpenClosed;
		SceneManager.sceneLoaded += OnSceneLoaded;
	}

	private void OnLobbyOpenClosed(bool _isOpen)
	{
		Refresh();
	}

	private void OnDestroy()
	{
		LobbyUIController.OpenCloseCallback -= OnLobbyOpenClosed;
		m_playerManager.EngagementChangeCallback -= OnEngagementChanged;
		SceneManager.sceneLoaded -= OnSceneLoaded;
	}

	private void OnEngagementChanged(EngagementSlot _e, GamepadUser _prev, GamepadUser _new)
	{
		if (_prev != null && _new != null)
		{
			IPlayerManager playerManager = GameUtils.RequireManagerInterface<IPlayerManager>();
			if (playerManager.GetUser(EngagementSlot.One) == _new)
			{
				T17EventSystem eventSystemForGamepadUser = T17EventSystemsManager.Instance.GetEventSystemForGamepadUser(_prev);
				SuppressionController suppressionController = new SuppressionController();
				if (eventSystemForGamepadUser != null)
				{
					eventSystemForGamepadUser.SuppressionController.MoveSuppressors(suppressionController);
					T17EventSystemsManager.Instance.ResetEventSystem(eventSystemForGamepadUser);
				}
				if (T17EventSystemsManager.Instance.GetEventSystemForGamepadUser(_new) == null)
				{
					T17EventSystem t17EventSystem = T17EventSystemsManager.Instance.AssignFreeEventSystemToGamepadUser(_new);
					suppressionController.MoveSuppressors(t17EventSystem.SuppressionController);
				}
			}
		}
		Refresh();
	}

	private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		if (mode != LoadSceneMode.Additive)
		{
			Refresh();
		}
	}

	private bool UIShouldOpen()
	{
		return m_playerManager.HasPlayer() && m_levelType == LevelType.WithoutChefs;
	}

	private void Refresh()
	{
		m_levelType = GetLevelType();
		m_playerManager.CanChangeSplitPads = m_levelType == LevelType.WithoutChefs;
		GameSession gameSession = GameUtils.GetGameSession();
		string text = SceneManager.GetActiveScene().name;
		bool usersSticky = m_levelType == LevelType.WithChefs || (gameSession != null && text.Equals(gameSession.TypeSettings.WorldMapScene)) || IsClientMode;
		SetUsersSticky(usersSticky);
	}

	private void SetUsersSticky(bool _sticky)
	{
		for (int i = 0; i < 4; i++)
		{
			if (i != 0)
			{
				GamepadUser user = m_playerManager.GetUser((EngagementSlot)i);
				FastList<User> users = ClientUserSystem.m_Users;
				User.MachineID s_LocalMachineId = ClientUserSystem.s_LocalMachineId;
				User user2 = UserSystemUtils.FindUser(users, null, s_LocalMachineId, (EngagementSlot)i);
				if (user != null && user2 != null)
				{
					user.StickyEngagement = _sticky;
				}
			}
		}
	}

	public static LevelType GetLevelType()
	{
		CampaignKitchenLoaderManager campaignKitchenLoaderManager = GameUtils.RequestManager<CampaignKitchenLoaderManager>();
		CompetitiveKitchenLoaderManager competitiveKitchenLoaderManager = GameUtils.RequestManager<CompetitiveKitchenLoaderManager>();
		string text = SceneManager.GetActiveScene().name;
		if (campaignKitchenLoaderManager == null && competitiveKitchenLoaderManager == null && text != "Loading" && text != "Credits")
		{
			return LevelType.WithoutChefs;
		}
		return LevelType.WithChefs;
	}
}
