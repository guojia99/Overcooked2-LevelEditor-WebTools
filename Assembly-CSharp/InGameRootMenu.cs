using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public class InGameRootMenu : RootMenu
{
	public enum IngameMenuTypeToOpen
	{
		WorldMapPause = 0,
		InLevelPause = 1,
		Controls = 2,
		Customisation = 3,
		GameMode = 4
	}

	[StructLayout(LayoutKind.Sequential, Size = 1)]
	public struct IngameMenuTypeToOpenComparer : IEqualityComparer<IngameMenuTypeToOpen>
	{
		public bool Equals(IngameMenuTypeToOpen x, IngameMenuTypeToOpen y)
		{
			return x == y;
		}

		public int GetHashCode(IngameMenuTypeToOpen obj)
		{
			return (int)obj;
		}
	}

	[HideInInspector]
	public IngameMenuTypeToOpen m_CurrentInGameMenuType;

	public Dictionary<IngameMenuTypeToOpen, MenuList_Container> m_InGameMenuTypes;

	private List<BaseMenuBehaviour> m_InGameMenuStack = new List<BaseMenuBehaviour>();

	private bool m_bClosingStack;

	private ILogicalButton m_uiCancelButton;

	private PlayerManager m_IPlayerManager;

	protected override void Awake()
	{
		m_RootMenuType = RootMenuType.InGame;
		m_CurrentInGameMenuType = IngameMenuTypeToOpen.WorldMapPause;
		base.Awake();
		m_InGameMenuTypes = new Dictionary<IngameMenuTypeToOpen, MenuList_Container>(default(IngameMenuTypeToOpenComparer));
		IngameMenuTypeToOpen[] array = (IngameMenuTypeToOpen[])Enum.GetValues(typeof(IngameMenuTypeToOpen));
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
			m_InGameMenuTypes.Add(array[i], menuList_Container);
		}
		m_uiCancelButton = PlayerInputLookup.GetEngagedButton(PlayerInputLookup.LogicalButtonID.UICancel, PlayerInputLookup.Player.One, PadSide.Both);
		m_IPlayerManager = GameUtils.RequireManager<PlayerManager>();
		m_IPlayerManager.EngagementChangeCallback += OnEngagementChanged;
	}

	protected override void OnDestroy()
	{
		m_IPlayerManager.EngagementChangeCallback -= OnEngagementChanged;
	}

	private void OnEngagementChanged(EngagementSlot _slot, GamepadUser _old, GamepadUser _new)
	{
		m_CurrentGamepadUser = _new;
	}

	private void OnMenuHide(BaseMenuBehaviour menu)
	{
		if (m_bClosingStack || m_InGameMenuStack.Count <= 0)
		{
			return;
		}
		if (m_InGameMenuStack[m_InGameMenuStack.Count - 1] != menu)
		{
			m_bClosingStack = true;
			int num = m_InGameMenuStack.IndexOf(menu);
			for (int num2 = m_InGameMenuStack.Count - 1; num2 >= num; num2--)
			{
				m_InGameMenuStack[num2].Hide();
				m_InGameMenuStack.RemoveAt(num2);
			}
			m_bClosingStack = false;
		}
		else
		{
			m_InGameMenuStack.Remove(menu);
		}
	}

	private void OnMenuShow(BaseMenuBehaviour menu)
	{
		m_InGameMenuStack.Add(menu);
	}

	public override BaseMenuBehaviour GetMenuOfType<T>()
	{
		for (int i = 0; i < m_InGameMenuTypes[m_CurrentInGameMenuType].m_Menus.Count; i++)
		{
			if (m_InGameMenuTypes[m_CurrentInGameMenuType].m_Menus[i].GetType() == typeof(T))
			{
				return m_InGameMenuTypes[m_CurrentInGameMenuType].m_Menus[i];
			}
		}
		return null;
	}

	public override int GetTabNumberOfType<T>()
	{
		for (int i = 0; i < m_InGameMenuTypes[m_CurrentInGameMenuType].m_Menus.Count; i++)
		{
			if (m_InGameMenuTypes[m_CurrentInGameMenuType].m_Menus[i].GetType() == typeof(T))
			{
				return i;
			}
		}
		return -1;
	}

	public override BaseMenuBehaviour GetCurrentOpenMenu()
	{
		if (m_InGameMenuStack.Count > 0)
		{
			return m_InGameMenuStack[m_InGameMenuStack.Count - 1];
		}
		return null;
	}

	public bool IsCurrentOpenMenuOfType(IngameMenuTypeToOpen _type)
	{
		MenuList_Container value;
		if (m_InGameMenuTypes != null && m_InGameMenuTypes.TryGetValue(_type, out value))
		{
			BaseMenuBehaviour currentOpenMenu = GetCurrentOpenMenu();
			for (int i = 0; i < value.m_Menus.Count; i++)
			{
				if (currentOpenMenu == value.m_Menus[i])
				{
					return true;
				}
			}
		}
		return false;
	}

	public override List<BaseMenuBehaviour> GetCurrentMenuSet()
	{
		return m_InGameMenuTypes[m_CurrentInGameMenuType].m_Menus;
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
			m_MainTabPanel.SetMenuBodies(m_InGameMenuTypes[m_CurrentInGameMenuType].m_Menus);
			num = m_InGameMenuTypes[m_CurrentInGameMenuType].m_DefaultTab;
			m_MainTabPanel.SetTabIndex(num);
		}
		else
		{
			m_InGameMenuTypes[m_CurrentInGameMenuType].m_Menus[m_InGameMenuTypes[m_CurrentInGameMenuType].m_DefaultTab].Show(currentGamer, this, null);
		}
		return true;
	}

	public override bool Hide(bool restoreInvokerState = true, bool isTabSwitch = false)
	{
		for (int num = m_InGameMenuStack.Count - 1; num >= 0; num--)
		{
			if (m_InGameMenuStack[num] != null)
			{
				m_InGameMenuStack[num].Hide();
			}
		}
		m_InGameMenuStack.Clear();
		return base.Hide(restoreInvokerState, isTabSwitch);
	}

	public void OpenInGameMenu(InGameMenuBehaviour menu)
	{
		foreach (KeyValuePair<IngameMenuTypeToOpen, MenuList_Container> inGameMenuType in m_InGameMenuTypes)
		{
			for (int i = 0; i < inGameMenuType.Value.m_Menus.Count; i++)
			{
				if (inGameMenuType.Value.m_Menus[i] == menu)
				{
					menu.Show(m_CurrentGamepadUser, this, base.gameObject, false);
					return;
				}
			}
		}
	}

	public bool OpenInGameMenu(IngameMenuTypeToOpen menuType, int index = -1)
	{
		index = ((index != -1) ? index : m_InGameMenuTypes[menuType].m_DefaultTab);
		m_CurrentInGameMenuType = menuType;
		return m_InGameMenuTypes[menuType].m_Menus[index].Show(m_CurrentGamepadUser, this, null);
	}

	public bool OpenInGameChildOfCurrent(int index)
	{
		if (index > 0 && index < m_InGameMenuTypes[m_CurrentInGameMenuType].m_Menus.Count && m_InGameMenuTypes[m_CurrentInGameMenuType].m_Menus[index].Show(m_CurrentGamepadUser, m_InGameMenuTypes[m_CurrentInGameMenuType].m_Menus[0], null))
		{
			RaiseMenuChangedEvent();
		}
		return false;
	}

	public bool IsChildMenuOpen()
	{
		if (m_RootMenuType == RootMenuType.InGame)
		{
			for (int num = m_InGameMenuTypes[m_CurrentInGameMenuType].m_Menus.Count - 1; num >= 1; num--)
			{
				if (m_InGameMenuTypes[m_CurrentInGameMenuType].m_Menus[num].gameObject.activeInHierarchy)
				{
					return true;
				}
			}
		}
		return false;
	}

	public BaseMenuBehaviour ReturnChildMenuOpen()
	{
		if (m_RootMenuType == RootMenuType.InGame)
		{
			for (int num = m_InGameMenuTypes[m_CurrentInGameMenuType].m_Menus.Count - 1; num >= 1; num--)
			{
				if (m_InGameMenuTypes[m_CurrentInGameMenuType].m_Menus[num].gameObject.activeInHierarchy)
				{
					return m_InGameMenuTypes[m_CurrentInGameMenuType].m_Menus[num];
				}
			}
		}
		return null;
	}

	public void DoNavigateOnUICancel()
	{
		InGameMenuBehaviour inGameMenuBehaviour = (InGameMenuBehaviour)ReturnChildMenuOpen();
		if (inGameMenuBehaviour == null)
		{
			inGameMenuBehaviour = (InGameMenuBehaviour)GetCurrentOpenMenu();
		}
		if (inGameMenuBehaviour != null)
		{
			inGameMenuBehaviour.InvokeNavigateOnUICancel();
		}
	}

	public void PromptGameExit()
	{
		T17DialogBox dialog = T17DialogBoxManager.GetDialog(true);
		if (dialog != null)
		{
			dialog.Initialize("Text.UI.Quit", "Text.Menu.OkToExit", "Text.Dialog.Prompt.Yes", "Text.Dialog.Prompt.No", string.Empty);
			dialog.OnConfirm = (T17DialogBox.DialogEvent)Delegate.Combine(dialog.OnConfirm, new T17DialogBox.DialogEvent(DoExit));
			dialog.Show();
		}
	}

	private void DoExit()
	{
		Application.Quit();
	}

	protected override void Update()
	{
		base.Update();
		if (m_uiCancelButton != null && m_uiCancelButton.JustReleased() && (m_CachedEventSystem == null || (m_CachedEventSystem != null && !m_CachedEventSystem.IsDisabled())) && (m_CurrentGamepadUser == null || !T17DialogBoxManager.HasDialogsForGamer(m_CurrentGamepadUser)))
		{
			InvokeUICancelWithStack(m_InGameMenuStack);
		}
	}
}
