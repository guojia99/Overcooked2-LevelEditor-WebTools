using System;
using System.Collections.Generic;
using Team17.Online;
using UnityEngine;

public class FrontendRootMenu : RootMenu
{
	public enum FrontendMenuTypeToOpen
	{
		MainMenu = 0,
		Campaign = 1,
		Coop = 2,
		Versus = 3,
		Customization = 4,
		Settings = 5,
		Switch = 6,
		SaveSelect = 7,
		DLC = 8,
		Popup = 9,
		SaveWait = 10
	}

	[HideInInspector]
	public FrontendMenuTypeToOpen m_CurrentFrontEndMenuType;

	public Dictionary<FrontendMenuTypeToOpen, MenuList_Container> m_FrontEndTabableMenuTypes;

	private List<BaseMenuBehaviour> m_FrontendMenuStack = new List<BaseMenuBehaviour>();

	public T17Text m_t17LegendText;

	public T17Text m_usernameText;

	private bool m_bClosingStack;

	private bool m_menuIsForHeadChef = true;

	private PlayerManager m_playerManager;

	private ILogicalButton m_uiCancelButton;

	protected override void Awake()
	{
		m_RootMenuType = RootMenuType.FrontEnd;
		m_CurrentFrontEndMenuType = FrontendMenuTypeToOpen.MainMenu;
		m_menuIsForHeadChef = true;
		base.Awake();
		m_FrontEndTabableMenuTypes = new Dictionary<FrontendMenuTypeToOpen, MenuList_Container>();
		FrontendMenuTypeToOpen[] array = (FrontendMenuTypeToOpen[])Enum.GetValues(typeof(FrontendMenuTypeToOpen));
		for (int i = 0; i < array.Length; i++)
		{
			MenuList_Container menuList_Container = new MenuList_Container();
			menuList_Container.m_Menus = m_EditorTabAbleMenuTypes[i].menus;
			for (int j = 0; j < menuList_Container.m_Menus.Count; j++)
			{
				if (menuList_Container.m_Menus[j] != null)
				{
					menuList_Container.m_Menus[j].Hide();
					BaseMenuBehaviour baseMenuBehaviour = menuList_Container.m_Menus[j];
					baseMenuBehaviour.OnShow = (BaseMenuBehaviourEvent)Delegate.Remove(baseMenuBehaviour.OnShow, new BaseMenuBehaviourEvent(OnMenuShow));
					BaseMenuBehaviour baseMenuBehaviour2 = menuList_Container.m_Menus[j];
					baseMenuBehaviour2.OnShow = (BaseMenuBehaviourEvent)Delegate.Combine(baseMenuBehaviour2.OnShow, new BaseMenuBehaviourEvent(OnMenuShow));
					BaseMenuBehaviour baseMenuBehaviour3 = menuList_Container.m_Menus[j];
					baseMenuBehaviour3.OnHide = (BaseMenuBehaviourEvent)Delegate.Remove(baseMenuBehaviour3.OnHide, new BaseMenuBehaviourEvent(OnMenuHide));
					BaseMenuBehaviour baseMenuBehaviour4 = menuList_Container.m_Menus[j];
					baseMenuBehaviour4.OnHide = (BaseMenuBehaviourEvent)Delegate.Combine(baseMenuBehaviour4.OnHide, new BaseMenuBehaviourEvent(OnMenuHide));
				}
			}
			menuList_Container.m_DefaultTab = m_EditorTabAbleMenuTypes[i].m_DefaultTab;
			m_FrontEndTabableMenuTypes.Add(array[i], menuList_Container);
		}
		if (ConnectionStatus.IsHost())
		{
			ServerUserSystem.RemoveMatchmadeUsers();
		}
		if (ConnectionStatus.IsHost() || !ConnectionStatus.IsInSession())
		{
			ServerUserSystem.ResetTeams();
		}
		InviteMonitor.SwitchHandlerType(InviteMonitor.HandlerType.Frontend);
		m_playerManager = GameUtils.RequireManager<PlayerManager>();
		m_uiCancelButton = PlayerInputLookup.GetEngagedButton(PlayerInputLookup.LogicalButtonID.UICancel, PlayerInputLookup.Player.One, PadSide.Both);
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		ClientUserSystem.usersChanged = (GenericVoid)Delegate.Remove(ClientUserSystem.usersChanged, new GenericVoid(UserEngagmentChanged));
	}

	protected override void Update()
	{
		base.Update();
		if (m_uiCancelButton != null && m_uiCancelButton.JustReleased() && (m_CachedEventSystem == null || (m_CachedEventSystem != null && !m_CachedEventSystem.IsDisabled())) && !T17DialogBoxManager.HasDialogsForGamer(m_CurrentGamepadUser))
		{
			InvokeUICancelWithStack(m_FrontendMenuStack);
		}
	}

	private void OnMenuHide(BaseMenuBehaviour menu)
	{
		if (m_bClosingStack)
		{
			return;
		}
		string text = null;
		if (m_FrontendMenuStack.Count > 0)
		{
			if (m_FrontendMenuStack[m_FrontendMenuStack.Count - 1] != menu)
			{
				m_bClosingStack = true;
				int num = m_FrontendMenuStack.IndexOf(menu);
				if (num != -1)
				{
					for (int num2 = m_FrontendMenuStack.Count - 1; num2 >= num; num2--)
					{
						m_FrontendMenuStack[num2].Hide();
						m_FrontendMenuStack.RemoveAt(num2);
					}
				}
				if (m_FrontendMenuStack.Count > 0)
				{
					text = m_FrontendMenuStack[0].GetLegendText();
				}
				m_bClosingStack = false;
			}
			else if (m_FrontendMenuStack[m_FrontendMenuStack.Count - 1].Hide() || !m_FrontendMenuStack[m_FrontendMenuStack.Count - 1].gameObject.activeInHierarchy)
			{
				m_FrontendMenuStack.Remove(menu);
				if (m_FrontendMenuStack.Count > 0)
				{
					text = m_FrontendMenuStack[m_FrontendMenuStack.Count - 1].GetLegendText();
				}
			}
		}
		if (!string.IsNullOrEmpty(text))
		{
			SetLegendText(text);
		}
	}

	private void OnMenuShow(BaseMenuBehaviour menu)
	{
		m_FrontendMenuStack.Add(menu);
		SetLegendText(menu.GetLegendText());
		SetUsernameText();
	}

	public void SetLegendText(string _legendText)
	{
		m_t17LegendText.SetLocalisedTextCatchAll(_legendText);
	}

	public void SetUsernameText()
	{
		if (m_usernameText == null)
		{
			return;
		}
		m_usernameText.text = string.Empty;
		if (m_playerManager != null)
		{
			GamepadUser user = m_playerManager.GetUser(EngagementSlot.One);
			if (user != null)
			{
				m_usernameText.text = user.DisplayName;
			}
		}
	}

	public override BaseMenuBehaviour GetMenuOfType<T>()
	{
		for (int i = 0; i < m_FrontEndTabableMenuTypes[m_CurrentFrontEndMenuType].m_Menus.Count; i++)
		{
			if (m_FrontEndTabableMenuTypes[m_CurrentFrontEndMenuType].m_Menus[i].GetType() == typeof(T))
			{
				return m_FrontEndTabableMenuTypes[m_CurrentFrontEndMenuType].m_Menus[i];
			}
		}
		return null;
	}

	public T SearchAllForMenuOfType<T>() where T : BaseMenuBehaviour
	{
		foreach (KeyValuePair<FrontendMenuTypeToOpen, MenuList_Container> frontEndTabableMenuType in m_FrontEndTabableMenuTypes)
		{
			MenuList_Container value = frontEndTabableMenuType.Value;
			for (int i = 0; i < value.m_Menus.Count; i++)
			{
				if (value.m_Menus[i] != null && value.m_Menus[i].GetType() == typeof(T))
				{
					return value.m_Menus[i] as T;
				}
			}
		}
		return (T)null;
	}

	public override int GetTabNumberOfType<T>()
	{
		for (int i = 0; i < m_FrontEndTabableMenuTypes[m_CurrentFrontEndMenuType].m_Menus.Count; i++)
		{
			if (m_FrontEndTabableMenuTypes[m_CurrentFrontEndMenuType].m_Menus[i].GetType() == typeof(T))
			{
				return i;
			}
		}
		return -1;
	}

	public override BaseMenuBehaviour GetCurrentOpenMenu()
	{
		int index = 0;
		if (m_MainTabPanel != null)
		{
			index = m_MainTabPanel.CurrentTabIndex;
		}
		return m_FrontEndTabableMenuTypes[m_CurrentFrontEndMenuType].m_Menus[index];
	}

	public override List<BaseMenuBehaviour> GetCurrentMenuSet()
	{
		return m_FrontEndTabableMenuTypes[m_CurrentFrontEndMenuType].m_Menus;
	}

	public override bool Show(GamepadUser currentGamer, BaseMenuBehaviour parent, GameObject invoker, bool hideInvoker = true)
	{
		if (!base.Show(currentGamer, parent, invoker, hideInvoker))
		{
			return false;
		}
		if (m_MainTabPanel != null)
		{
			m_MainTabPanel.Show(currentGamer, this, null);
			int num = 0;
			m_MainTabPanel.SetMenuBodies(m_FrontEndTabableMenuTypes[m_CurrentFrontEndMenuType].m_Menus);
			num = m_FrontEndTabableMenuTypes[m_CurrentFrontEndMenuType].m_DefaultTab;
			ConfigureTabMenuForHeadChef();
			m_MainTabPanel.AttemptToSetTabIndex(num);
		}
		else
		{
			m_FrontEndTabableMenuTypes[m_CurrentFrontEndMenuType].m_Menus[m_FrontEndTabableMenuTypes[m_CurrentFrontEndMenuType].m_DefaultTab].Show(currentGamer, this, null);
		}
		ClientUserSystem.usersChanged = (GenericVoid)Delegate.Combine(ClientUserSystem.usersChanged, new GenericVoid(UserEngagmentChanged));
		return true;
	}

	public override bool Hide(bool restoreInvokerState = true, bool isTabSwitch = false)
	{
		for (int num = m_FrontendMenuStack.Count - 1; num >= 0; num--)
		{
			if (m_FrontendMenuStack[num] != null)
			{
				m_FrontendMenuStack[num].Hide();
			}
		}
		m_FrontendMenuStack.Clear();
		ClientUserSystem.usersChanged = (GenericVoid)Delegate.Remove(ClientUserSystem.usersChanged, new GenericVoid(UserEngagmentChanged));
		return base.Hide(restoreInvokerState, isTabSwitch);
	}

	public void CollapseCurrentTab()
	{
		if (m_MainTabPanel != null)
		{
			m_MainTabPanel.CollapseCurrentTab();
		}
	}

	public void ExpandCurrentTab()
	{
		if (m_MainTabPanel != null)
		{
			m_MainTabPanel.ExpandCurrentTab();
		}
	}

	public void OpenFrontendMenu(FrontendMenuBehaviour menu)
	{
		foreach (KeyValuePair<FrontendMenuTypeToOpen, MenuList_Container> frontEndTabableMenuType in m_FrontEndTabableMenuTypes)
		{
			for (int i = 0; i < frontEndTabableMenuType.Value.m_Menus.Count; i++)
			{
				if (frontEndTabableMenuType.Value.m_Menus[i] == menu)
				{
					menu.Show(m_CurrentGamepadUser, this, base.gameObject, false);
					return;
				}
			}
		}
	}

	public bool OpenFrontendChildOfCurrent(int index)
	{
		if (index > 0 && index < m_FrontEndTabableMenuTypes[m_CurrentFrontEndMenuType].m_Menus.Count && m_FrontEndTabableMenuTypes[m_CurrentFrontEndMenuType].m_Menus[index].Show(m_CurrentGamepadUser, m_FrontEndTabableMenuTypes[m_CurrentFrontEndMenuType].m_Menus[0], null))
		{
			RaiseMenuChangedEvent();
		}
		return false;
	}

	public bool IsChildMenuOpen()
	{
		if (m_RootMenuType == RootMenuType.FrontEnd)
		{
			for (int num = m_FrontEndTabableMenuTypes[m_CurrentFrontEndMenuType].m_Menus.Count - 1; num >= 1; num--)
			{
				if (m_FrontEndTabableMenuTypes[m_CurrentFrontEndMenuType].m_Menus[num].gameObject.activeInHierarchy)
				{
					return true;
				}
			}
		}
		return false;
	}

	public BaseMenuBehaviour ReturnChildMenuOpen()
	{
		if (m_RootMenuType == RootMenuType.FrontEnd)
		{
			for (int num = m_FrontEndTabableMenuTypes[m_CurrentFrontEndMenuType].m_Menus.Count - 1; num >= 1; num--)
			{
				if (m_FrontEndTabableMenuTypes[m_CurrentFrontEndMenuType].m_Menus[num].gameObject.activeInHierarchy)
				{
					return m_FrontEndTabableMenuTypes[m_CurrentFrontEndMenuType].m_Menus[num];
				}
			}
		}
		return null;
	}

	public void DoNavigateOnUICancel()
	{
		if (m_RootMenuType != RootMenuType.FrontEnd)
		{
			return;
		}
		BaseMenuBehaviour baseMenuBehaviour = null;
		foreach (KeyValuePair<FrontendMenuTypeToOpen, MenuList_Container> frontEndTabableMenuType in m_FrontEndTabableMenuTypes)
		{
			List<BaseMenuBehaviour> menus = frontEndTabableMenuType.Value.m_Menus;
			for (int num = menus.Count - 1; num >= 0; num--)
			{
				if (menus[num] != null && menus[num].gameObject != null && menus[num].gameObject.activeInHierarchy && menus[num].InvokeNavigateOnUICancel())
				{
					baseMenuBehaviour = menus[num];
					break;
				}
			}
			if (baseMenuBehaviour != null)
			{
				break;
			}
		}
		if (baseMenuBehaviour == null && InvokeNavigateOnUICancel())
		{
			baseMenuBehaviour = this;
		}
	}

	private void UserEngagmentChanged()
	{
		ConfigureTabMenuForHeadChef();
	}

	private bool ConfigureTabMenuForHeadChef()
	{
		bool flag = !ConnectionStatus.IsInSession() || ConnectionStatus.IsHost();
		if (flag != m_menuIsForHeadChef)
		{
			m_menuIsForHeadChef = flag;
			if (m_MainTabPanel != null)
			{
				m_MainTabPanel.SetMenuEnabled(0, flag);
				m_MainTabPanel.SetMenuEnabled(1, flag);
				m_MainTabPanel.SetMenuEnabled(2, flag);
			}
			if (!flag && T17FrontendFlow.Instance.m_PlayerLobby != null && !T17FrontendFlow.Instance.m_PlayerLobby.IsFocused && m_MainTabPanel != null)
			{
				m_MainTabPanel.AttemptToSetTabIndex(m_MainTabPanel.CurrentTabIndex);
			}
			return true;
		}
		return false;
	}

	public void HideMenuStack()
	{
		for (int num = m_FrontendMenuStack.Count - 1; num >= 0; num--)
		{
			m_FrontendMenuStack[num].Hide();
		}
		m_FrontendMenuStack.Clear();
	}
}
