using System;
using Team17.Online;
using UnityEngine;

public class T17InGameFlow : MonoBehaviour
{
	private static T17InGameFlow s_Instance;

	[SerializeField]
	public InGameRootMenu m_Rootmenu;

	[SerializeField]
	public GameObject m_BackgroundImage;

	[SerializeField]
	private InGamePauseMenu m_pauseMenu;

	private IPlayerManager m_IPlayerManager;

	private CallbackVoid m_DoThisOnClose;

	private GameObject m_ObjectBeforeFocusSwitch;

	private GameObject m_EventSystem;

	private bool m_bInRoundResults;

	private bool m_wasPauseMenuOpen;

	public static T17InGameFlow Instance
	{
		get
		{
			return s_Instance;
		}
	}

	public bool WasPauseMenuOpen
	{
		get
		{
			return m_wasPauseMenuOpen;
		}
	}

	public void Awake()
	{
		if (s_Instance != null)
		{
			UnityEngine.Object.Destroy(this);
		}
		else
		{
			s_Instance = this;
		}
		if (m_BackgroundImage != null)
		{
			m_BackgroundImage.SetActive(false);
		}
		m_IPlayerManager = GameUtils.RequireManagerInterface<IPlayerManager>();
		GameObject gameMetaEnvironment = GameUtils.GetGameMetaEnvironment();
		m_EventSystem = GameObject.Find("EventSystems");
		if (m_EventSystem != null)
		{
			m_EventSystem.SetActive(false);
		}
	}

	private void OnDestroy()
	{
		if (s_Instance != null)
		{
			UnityEngine.Object.Destroy(s_Instance);
			s_Instance = null;
		}
		if (m_EventSystem != null)
		{
			m_EventSystem.SetActive(true);
		}
	}

	private void Start()
	{
		if (!m_IPlayerManager.HasPlayer())
		{
		}
		GamepadUser user = m_IPlayerManager.GetUser(EngagementSlot.One);
		if (m_Rootmenu != null)
		{
			m_Rootmenu.Show(user, null, null);
		}
	}

	private void Update()
	{
		if (m_EventSystem != null)
		{
			if (T17DialogBoxManager.HasAnyOpenDialogs() || m_Rootmenu.GetCurrentOpenMenu() != null || m_bInRoundResults)
			{
				m_EventSystem.SetActive(true);
			}
			else
			{
				m_EventSystem.SetActive(false);
			}
		}
		if (m_wasPauseMenuOpen)
		{
			m_wasPauseMenuOpen = IsPauseMenuOpen();
		}
	}

	public void OpenPauseMenu(bool isWorldMap, PlayerInputLookup.Player playerThatRequestedPause = PlayerInputLookup.Player.One)
	{
		if ((int)playerThatRequestedPause < ClientUserSystem.m_Users.Count)
		{
			User user = ClientUserSystem.m_Users._items[(int)playerThatRequestedPause];
			GamepadUser gamepadUser = ((user == null) ? null : user.GamepadUser);
			InGameRootMenu.IngameMenuTypeToOpen ingameMenuTypeToOpen = ((!isWorldMap) ? InGameRootMenu.IngameMenuTypeToOpen.InLevelPause : InGameRootMenu.IngameMenuTypeToOpen.WorldMapPause);
			if (m_EventSystem != null)
			{
				m_EventSystem.SetActive(true);
			}
			if (!IsPauseMenuOpen())
			{
				m_pauseMenu.Show(gamepadUser, null, null);
				m_pauseMenu.SetPauseType(isWorldMap);
			}
			if (m_BackgroundImage != null)
			{
				m_BackgroundImage.SetActive(true);
			}
			m_wasPauseMenuOpen = true;
		}
	}

	public bool IsPauseMenuOpen()
	{
		return m_Rootmenu.IsCurrentOpenMenuOfType(InGameRootMenu.IngameMenuTypeToOpen.InLevelPause) || m_Rootmenu.IsCurrentOpenMenuOfType(InGameRootMenu.IngameMenuTypeToOpen.WorldMapPause);
	}

	public void RequestClosePauseMenu()
	{
		if (m_Rootmenu.IsCurrentOpenMenuOfType(InGameRootMenu.IngameMenuTypeToOpen.WorldMapPause) || m_Rootmenu.IsCurrentOpenMenuOfType(InGameRootMenu.IngameMenuTypeToOpen.InLevelPause))
		{
			ClosePauseMenu();
		}
	}

	public void ClosePauseMenu()
	{
		if (m_pauseMenu != null)
		{
			m_pauseMenu.Hide();
		}
		if (m_BackgroundImage != null)
		{
			m_BackgroundImage.SetActive(false);
		}
		if (m_DoThisOnClose != null)
		{
			m_DoThisOnClose();
		}
	}

	public void RegisterWhatToDoOnPauseMenuClose(CallbackVoid doThis)
	{
		m_DoThisOnClose = doThis;
	}

	public void RegisterOnPauseMenuVisibilityChanged(BaseMenuBehaviour.BaseMenuBehaviourEvent _callback)
	{
		if (m_pauseMenu != null)
		{
			InGamePauseMenu pauseMenu = m_pauseMenu;
			pauseMenu.OnShow = (BaseMenuBehaviour.BaseMenuBehaviourEvent)Delegate.Combine(pauseMenu.OnShow, _callback);
			InGamePauseMenu pauseMenu2 = m_pauseMenu;
			pauseMenu2.OnHide = (BaseMenuBehaviour.BaseMenuBehaviourEvent)Delegate.Combine(pauseMenu2.OnHide, _callback);
		}
	}

	public void UnRegisterOnPauseMenuVisibilityChanged(BaseMenuBehaviour.BaseMenuBehaviourEvent _callback)
	{
		if (m_pauseMenu != null)
		{
			InGamePauseMenu pauseMenu = m_pauseMenu;
			pauseMenu.OnShow = (BaseMenuBehaviour.BaseMenuBehaviourEvent)Delegate.Remove(pauseMenu.OnShow, _callback);
			InGamePauseMenu pauseMenu2 = m_pauseMenu;
			pauseMenu2.OnHide = (BaseMenuBehaviour.BaseMenuBehaviourEvent)Delegate.Remove(pauseMenu2.OnHide, _callback);
		}
	}

	public void SetInRoundResults(bool bState)
	{
		m_bInRoundResults = bState;
	}
}
