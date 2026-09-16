using System;
using System.Collections.Generic;
using Team17.Online;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(TimeManager))]
public class PauseMenuManager : Manager
{
	private enum MenuEntries
	{
		Resume = 0,
		Restart = 1,
		Quit = 2,
		Controls = 3
	}

	private class SessionData
	{
		public ILogicalButton m_uiUpButton;

		public ILogicalButton m_uiDownButton;

		public ILogicalButton m_uiSelectButton;

		public ILogicalButton m_uiCancelButton;

		public SessionData(GameObject _startGUIPrefab, string _canvas, MenuEntries[] _menuEntries)
		{
			m_uiUpButton = BuildButton(PlayerInputLookup.LogicalButtonID.UIUp);
			m_uiDownButton = BuildButton(PlayerInputLookup.LogicalButtonID.UIDown);
			m_uiSelectButton = BuildButton(PlayerInputLookup.LogicalButtonID.UISelect);
			m_uiCancelButton = BuildButton(PlayerInputLookup.LogicalButtonID.UICancel);
		}
	}

	[SerializeField]
	private GameObject m_startGUIPrefab;

	[SerializeField]
	private string m_uiCanvasName = "ScalingHUDCanvas";

	[SerializeField]
	private MenuEntries[] m_entries = new MenuEntries[3]
	{
		MenuEntries.Resume,
		MenuEntries.Restart,
		MenuEntries.Quit
	};

	[SerializeField]
	[AssignResource("ControlsScreen", Editorbility.NonEditable)]
	private Image m_controlsScreenPrefab;

	[SerializeField]
	[AssignResource("UnsidedAmbiControlsMappingData", Editorbility.NonEditable)]
	private AmbiControlsMappingData m_unsidedAmbiMapping;

	private ILogicalButton[] m_startButtons;

	private SessionData m_sessionData;

	private TimeManager m_timeManager;

	private GameObject m_optionsInstance;

	private PlayerManager m_playerManager;

	private bool m_awaitDestroy;

	private TimeManager.PauseLayer[] m_PausedLayers;

	private bool m_holdingDownFromOpen;

	private void Start()
	{
		m_timeManager = base.gameObject.RequireComponent<TimeManager>();
		m_playerManager = GameUtils.RequireManager<PlayerManager>();
		m_startButtons = BuildStartButtons();
		ClaimStartPressEvent();
		ClientUserSystem.usersChanged = (GenericVoid)Delegate.Combine(ClientUserSystem.usersChanged, new GenericVoid(OnUsersChanged));
	}

	private void OnUsersChanged()
	{
		m_startButtons = BuildStartButtons();
	}

	private void OnDestroy()
	{
		if (m_sessionData != null)
		{
			Close();
		}
		ClientUserSystem.usersChanged = (GenericVoid)Delegate.Remove(ClientUserSystem.usersChanged, new GenericVoid(OnUsersChanged));
	}

	private void OnDisable()
	{
		if (m_sessionData != null && T17InGameFlow.Instance != null)
		{
			T17InGameFlow.Instance.RequestClosePauseMenu();
		}
	}

	private ILogicalButton[] BuildStartButtons()
	{
		ILogicalButton[] array = new ILogicalButton[4];
		int num = 0;
		for (int i = 0; i < array.Length; i++)
		{
			ILogicalButton logicalButton;
			if (i < ClientUserSystem.m_Users.Count && ClientUserSystem.m_Users._items[i].IsLocal)
			{
				logicalButton = PlayerInputLookup.GetEngagedButton(PlayerInputLookup.LogicalButtonID.Pause, (PlayerInputLookup.Player)num, PadSide.Both);
				num++;
			}
			else
			{
				logicalButton = new ComboLogicalButton(new ILogicalButton[0]);
			}
			array[i] = logicalButton;
		}
		return array;
	}

	private void ClaimStartPressEvent()
	{
		for (int i = 0; i < m_startButtons.Length; i++)
		{
			m_startButtons[i].ClaimPressEvent();
		}
	}

	private PlayerInputLookup.Player GetFirstJustPressedStartButton()
	{
		PlayerInputLookup.Player player = PlayerInputLookup.Player.Count;
		for (int i = 0; i < m_startButtons.Length; i++)
		{
			if (m_startButtons[i].JustPressed() && player == PlayerInputLookup.Player.Count)
			{
				player = (PlayerInputLookup.Player)i;
			}
		}
		return player;
	}

	private static ILogicalButton BuildButton(PlayerInputLookup.LogicalButtonID _buttonID)
	{
		OvercookedEngagementController.LevelType levelType = OvercookedEngagementController.GetLevelType();
		FastList<User> fastList = new FastList<User>();
		for (int i = 0; i < ClientUserSystem.m_Users.Count; i++)
		{
			if (ClientUserSystem.m_Users._items[i].IsLocal)
			{
				fastList.Add(ClientUserSystem.m_Users._items[i]);
			}
		}
		return PlayerInputLookup.GetAnyButton(_buttonID, PadSide.Both);
	}

	private void Update()
	{
		if (!m_awaitDestroy)
		{
			if (m_sessionData == null)
			{
				UpdateWhileClosed();
			}
			else
			{
				UpdateWhileOpen();
			}
		}
	}

	private void UpdateWhileClosed()
	{
		PlayerInputLookup.Player firstJustPressedStartButton = GetFirstJustPressedStartButton();
		if (TimeManager.IsPaused(TimeManager.PauseLayer.System) || TimeManager.IsPaused(TimeManager.PauseLayer.Network) || firstJustPressedStartButton != PlayerInputLookup.Player.Count)
		{
			m_holdingDownFromOpen = true;
			Pause(firstJustPressedStartButton);
		}
	}

	private void Pause(PlayerInputLookup.Player _playerThatRequestedPause = PlayerInputLookup.Player.One)
	{
		if (!(T17InGameFlow.Instance != null) || !(T17InGameFlow.Instance.m_Rootmenu.GetCurrentOpenMenu() == null))
		{
			return;
		}
		GameSession gameSession = GameUtils.GetGameSession();
		if (!(gameSession != null))
		{
			return;
		}
		m_sessionData = new SessionData(m_startGUIPrefab, m_uiCanvasName, m_entries);
		m_PausedLayers = GetLayersToPause();
		for (int i = 0; i < m_PausedLayers.Length; i++)
		{
			m_timeManager.SetPaused(m_PausedLayers[i], true, this);
		}
		if (_playerThatRequestedPause == PlayerInputLookup.Player.Count)
		{
			FastList<User> users = ClientUserSystem.m_Users;
			for (int j = 0; j < users.Count; j++)
			{
				User user = users._items[j];
				if (user.IsLocal && user.GamepadUser == null)
				{
					_playerThatRequestedPause = (PlayerInputLookup.Player)j;
					break;
				}
			}
		}
		GameSession.GameTypeSettings typeSettings = gameSession.TypeSettings;
		T17InGameFlow.Instance.OpenPauseMenu(SceneManager.GetActiveScene().name == typeSettings.WorldMapScene, _playerThatRequestedPause);
		T17InGameFlow.Instance.RegisterWhatToDoOnPauseMenuClose(Close);
	}

	private TimeManager.PauseLayer[] GetLayersToPause()
	{
		if (ConnectionStatus.IsInSession() && UserSystemUtils.AnyRemoteUsers())
		{
			return new TimeManager.PauseLayer[1] { TimeManager.PauseLayer.Network };
		}
		return new TimeManager.PauseLayer[2]
		{
			TimeManager.PauseLayer.Main,
			TimeManager.PauseLayer.UI
		};
	}

	private void ConsumeAllButtons()
	{
		ClaimStartPressEvent();
		if (m_sessionData != null)
		{
			m_sessionData.m_uiSelectButton.ClaimPressEvent();
			m_sessionData.m_uiSelectButton.ClaimReleaseEvent();
			m_sessionData.m_uiCancelButton.ClaimPressEvent();
			m_sessionData.m_uiCancelButton.ClaimReleaseEvent();
			m_sessionData.m_uiDownButton.ClaimPressEvent();
			m_sessionData.m_uiDownButton.ClaimReleaseEvent();
			m_sessionData.m_uiUpButton.ClaimPressEvent();
			m_sessionData.m_uiUpButton.ClaimReleaseEvent();
		}
	}

	private void UpdateWhileOpen()
	{
		if (TimeManager.IsPaused(TimeManager.PauseLayer.System) || m_holdingDownFromOpen)
		{
			if (!m_sessionData.m_uiCancelButton.IsDown())
			{
				m_holdingDownFromOpen = false;
			}
			ConsumeAllButtons();
			return;
		}
		bool flag = GetFirstJustPressedStartButton() != PlayerInputLookup.Player.Count || m_sessionData.m_uiCancelButton.JustReleased();
		bool flag2 = T17DialogBoxManager.HasAnyOpenDialogs() || GameUtils.RequireManager<PlayerManager>().IsWarningActive(PlayerWarning.Disengaged);
		if (flag && !flag2)
		{
			T17InGameFlow.Instance.RequestClosePauseMenu();
		}
	}

	private void OnControlsSelected()
	{
		if (m_uiCanvasName == "ScalingHUDCanvas")
		{
			m_optionsInstance = GameUtils.InstantiateUIControllerOnScalingHUDCanvas(m_controlsScreenPrefab.gameObject);
		}
		else
		{
			m_optionsInstance = GameUtils.InstantiateUIController(m_controlsScreenPrefab.gameObject, m_uiCanvasName);
		}
	}

	private void Close()
	{
		m_sessionData = null;
		for (int i = 0; i < m_PausedLayers.Length; i++)
		{
			m_timeManager.SetPaused(m_PausedLayers[i], false, this);
		}
		m_PausedLayers = null;
		ClaimStartPressEvent();
	}

	private static FrontendListEntry.NameData[] GetMenuNames(MenuEntries[] _menuEntries)
	{
		return _menuEntries.ConvertAll((MenuEntries x) => new FrontendListEntry.NameData("PauseMenu." + x, true, false));
	}
}
