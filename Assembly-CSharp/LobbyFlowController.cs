using System;
using Team17.Online;
using UnityEngine;

public class LobbyFlowController : MonoBehaviour
{
	public enum LobbyState
	{
		PreSetup = 0,
		Matchmake = 1,
		LocalSetup = 2,
		OnlineSetup = 3,
		LocalThemeSelection = 4,
		OnlineThemeSelection = 5,
		LocalThemeSelected = 6,
		OnlineThemeSelected = 7
	}

	public class ThemeChoice
	{
		public SceneDirectoryData.LevelTheme m_theme = SceneDirectoryData.LevelTheme.Count;

		public int m_chefIndex = -1;
	}

	[Serializable]
	public class LobbyName
	{
		public GameObject m_coop;

		public GameObject m_versus;

		public void Update(bool _isCoop)
		{
			m_coop.SetActive(_isCoop);
			m_versus.SetActive(!_isCoop);
		}
	}

	[Serializable]
	public class StateStrings
	{
		public enum State
		{
			None = 0,
			ChooseTheme = 1,
			WaitingForOthers = 2,
			PickingLevel = 3
		}

		public GameObject m_chooseTheme;

		public GameObject m_waitingOthers;

		public GameObject m_pickingLevel;

		public void Update(State _state)
		{
			m_chooseTheme.SetActive(_state == State.ChooseTheme);
			m_waitingOthers.SetActive(_state == State.WaitingForOthers);
			m_pickingLevel.SetActive(_state == State.PickingLevel);
		}
	}

	[Serializable]
	public class DLCSerializedGameSessionData : DLCSerializedData<GameSession>
	{
	}

	private static LobbyFlowController s_instance;

	public LobbyName m_lobbyNames;

	public StateStrings m_stateStrings;

	public T17Text m_timerText;

	public GameObject m_chosenThemes;

	public ThemeChoiceElement[] m_themeSelections = new ThemeChoiceElement[4];

	[HideInInspector]
	public ThemeSelectButton m_selectedTheme;

	public float m_themeSelectionDuration = 2f;

	public GameObject m_localPlayerNotification;

	public GameObject m_netPlayerNotification;

	public ThemeSelectRootMenu m_themeSelectMenu;

	public UIPlayerRootMenu m_uiPlayerRoot;

	[SerializeField]
	public float m_timeLimit = 45f;

	[SerializeField]
	[AssignResource("No_Team", Editorbility.Editable)]
	public ChefColourData m_noTeam;

	[SerializeField]
	[AssignResource("Red", Editorbility.Editable)]
	public ChefColourData m_red;

	[SerializeField]
	[AssignResource("Blue", Editorbility.Editable)]
	public ChefColourData m_blue;

	[HideInInspector]
	public uint m_noTeamColourIndex = 7u;

	[HideInInspector]
	public uint m_redTeamColourIndex = 7u;

	[HideInInspector]
	public uint m_blueTeamColourIndex = 7u;

	[Space]
	[Header("Game Sessions")]
	[SerializeField]
	public DLCSerializedGameSessionData m_CoopGameSessionData = new DLCSerializedGameSessionData();

	[SerializeField]
	public DLCSerializedGameSessionData m_CompetitiveGameSessionData = new DLCSerializedGameSessionData();

	private GameSession[] m_CoopGameSessionPrefabs;

	private GameSession[] m_CompetitiveGameSessionPrefabs;

	private SceneDirectoryData[] m_coopSceneDirectories;

	private SceneDirectoryData[] m_vsSceneDirectories;

	[Space]
	[SerializeField]
	public float m_selectedEndScale = 1.3f;

	public const int c_ThemeSelectionSteps = 40;

	public string m_versusLegend;

	public string m_CoopLegend;

	public string m_VersusLegendNoEmote;

	public string m_CoopLegendNoEmote;

	public T17Text m_legend;

	public static LobbyFlowController Instance
	{
		get
		{
			return s_instance;
		}
	}

	private void Awake()
	{
		if (s_instance != null)
		{
			UnityEngine.Object.Destroy(this);
		}
		else
		{
			s_instance = this;
		}
		m_noTeamColourIndex = GetColourIndex(m_noTeam);
		m_redTeamColourIndex = GetColourIndex(m_red);
		m_blueTeamColourIndex = GetColourIndex(m_blue);
		SetupSceneDirectories();
		if (base.gameObject.GetComponent<ClientLobbyFlowController>() == null)
		{
			base.gameObject.AddComponent<ClientLobbyFlowController>();
		}
		PlayerInputLookup.ResetToDefaultInputConfig();
		InviteMonitor.SwitchHandlerType(InviteMonitor.HandlerType.Gameplay);
	}

	private void Start()
	{
		DisableUnusableThemes();
	}

	public bool IsLocalState(LobbyState _state)
	{
		return _state == LobbyState.LocalSetup || _state == LobbyState.LocalThemeSelection || _state == LobbyState.LocalThemeSelected;
	}

	private void OnDestroy()
	{
		if (s_instance == this)
		{
			s_instance = null;
		}
	}

	public void UpdateLegend(bool coop)
	{
		if (coop)
		{
			if (!UserSystemUtils.AnySplitPadUsers())
			{
				m_legend.SetLocalisedTextCatchAll(m_CoopLegend);
			}
			else
			{
				m_legend.SetLocalisedTextCatchAll(m_CoopLegendNoEmote);
			}
		}
		else if (!UserSystemUtils.AnySplitPadUsers())
		{
			m_legend.SetLocalisedTextCatchAll(m_versusLegend);
		}
		else
		{
			m_legend.SetLocalisedTextCatchAll(m_VersusLegendNoEmote);
		}
	}

	public uint GetColourIndex(ChefColourData _colourData)
	{
		AvatarDirectoryData avatarDirectoryData = GameUtils.GetAvatarDirectoryData();
		for (uint num = 0u; num < avatarDirectoryData.Colours.Length; num++)
		{
			if (avatarDirectoryData.Colours[num] == _colourData)
			{
				return num;
			}
		}
		return 7u;
	}

	public void RefreshUserColour(User _user, bool _isCoop)
	{
		if (_user == null)
		{
			return;
		}
		uint colour = _user.Colour;
		if (_isCoop)
		{
			AvatarDirectoryData avatarDirectoryData = GameUtils.GetAvatarDirectoryData();
			if (avatarDirectoryData != null)
			{
				int num = ClientUserSystem.m_Users._items.FindIndex_Predicate((User u) => u == _user);
				if (num != -1)
				{
					colour = (uint)num;
				}
			}
		}
		else
		{
			switch (_user.Team)
			{
			case TeamID.None:
				colour = m_noTeamColourIndex;
				break;
			case TeamID.One:
				colour = m_redTeamColourIndex;
				break;
			case TeamID.Two:
				colour = m_blueTeamColourIndex;
				break;
			}
		}
		_user.Colour = colour;
	}

	public void RefreshUserColours(bool _isCoop)
	{
		for (int i = 0; i < ClientUserSystem.m_Users.Count; i++)
		{
			RefreshUserColour(ClientUserSystem.m_Users._items[i], _isCoop);
		}
	}

	public bool UnanimousSelection(ThemeChoice[] _userChoices)
	{
		SceneDirectoryData.LevelTheme levelTheme = SceneDirectoryData.LevelTheme.Count;
		for (int i = 0; i < ClientUserSystem.m_Users.Count; i++)
		{
			ThemeChoice themeChoice = _userChoices[i];
			if (themeChoice != null)
			{
				if (themeChoice.m_theme == SceneDirectoryData.LevelTheme.Count || themeChoice.m_theme == SceneDirectoryData.LevelTheme.Null)
				{
					return false;
				}
				if (levelTheme == SceneDirectoryData.LevelTheme.Count)
				{
					levelTheme = themeChoice.m_theme;
				}
				else if (levelTheme != themeChoice.m_theme)
				{
					return false;
				}
			}
		}
		return true;
	}

	private bool SessionListCleanUp(GameSession _session)
	{
		if (_session == null)
		{
			return true;
		}
		return false;
	}

	private void SetupSceneDirectories()
	{
		Converter<GameSession, SceneDirectoryData> converter = delegate(GameSession _session)
		{
			GameProgress gameProgress = _session.gameObject.RequireComponentRecursive<GameProgress>();
			return gameProgress.GetSceneDirectory();
		};
		m_CoopGameSessionPrefabs = m_CoopGameSessionData.AllData.AllRemoved_Predicate(SessionListCleanUp);
		m_coopSceneDirectories = m_CoopGameSessionPrefabs.ConvertAll(converter).AllRemoved_Predicate((SceneDirectoryData x) => x == null);
		m_CompetitiveGameSessionPrefabs = m_CompetitiveGameSessionData.AllData.AllRemoved_Predicate(SessionListCleanUp);
		m_vsSceneDirectories = m_CompetitiveGameSessionPrefabs.ConvertAll(converter).AllRemoved_Predicate((SceneDirectoryData x) => x == null);
	}

	public SceneDirectoryData[] GetSceneDirectories()
	{
		switch (ClientGameSetup.Mode)
		{
		case GameMode.Party:
			return m_coopSceneDirectories;
		case GameMode.Versus:
			return m_vsSceneDirectories;
		default:
			return null;
		}
	}

	public GameSession CreateLobbySession(GameSession.GameType _gameType, int _dlcID)
	{
		GameSession gameSession = GameUtils.GetGameSession();
		if (gameSession != null)
		{
			UnityEngine.Object.DestroyImmediate(gameSession.gameObject);
		}
		GameSession[] array = null;
		switch (_gameType)
		{
		case GameSession.GameType.Cooperative:
			array = m_CoopGameSessionPrefabs;
			break;
		case GameSession.GameType.Competitive:
			array = m_CompetitiveGameSessionPrefabs;
			break;
		}
		GameSession gameSession2 = Array.Find(array, (GameSession x) => x.DLC == _dlcID);
		if (gameSession2 != null)
		{
			GameObject gameObject = gameSession2.gameObject.InstantiateOnParent(null);
			gameObject.name = string.Format("LobbyGameSession_{0}_{1}", _gameType.ToString(), _dlcID);
			GameSession gameSession3 = gameObject.RequireComponent<GameSession>();
			gameSession3.TypeSettings.Type = _gameType;
			gameSession3.TypeSettings.WorldMapScene = "Lobbies";
			return gameSession3;
		}
		return null;
	}

	public int GetDLCIDFromSceneDirIndex(GameSession.GameType _gameType, int _idx)
	{
		GameSession[] array = null;
		switch (_gameType)
		{
		case GameSession.GameType.Cooperative:
			array = m_CoopGameSessionPrefabs;
			break;
		case GameSession.GameType.Competitive:
			array = m_CompetitiveGameSessionPrefabs;
			break;
		}
		return array[_idx].DLC;
	}

	private void DisableUnusableThemes()
	{
		int num = 64;
		SceneDirectoryData[] sceneDirectories = GetSceneDirectories();
		foreach (SceneDirectoryData sceneDirectoryData in sceneDirectories)
		{
			for (int j = 0; j < sceneDirectoryData.Scenes.Length; j++)
			{
				num |= 1 << (int)sceneDirectoryData.Scenes[j].Theme;
			}
		}
		for (int k = 0; k < 22; k++)
		{
			SceneDirectoryData.LevelTheme levelTheme = (SceneDirectoryData.LevelTheme)k;
			if (!MaskUtils.HasFlag(num, levelTheme))
			{
				m_themeSelectMenu.DisallowTheme(levelTheme);
			}
		}
	}
}
