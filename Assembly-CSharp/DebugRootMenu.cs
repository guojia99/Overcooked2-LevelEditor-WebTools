using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class DebugRootMenu : FrontendMenuBehaviour
{
	public enum RootMenuType
	{
		FrontEnd = 0,
		InGame = 1,
		HUD = 2,
		Results = 3,
		LevelEditor = 4,
		Lobby = 5
	}

	[Serializable]
	public class EditorHack_BaseMenuBehaviour
	{
		public int m_DefaultTab;

		public bool m_bIsExpanded;

		public List<BaseMenuBehaviour> menus;
	}

	[Serializable]
	public class MenuList_Container
	{
		public int m_DefaultTab;

		public List<BaseMenuBehaviour> m_Menus;
	}

	public T17TabPanel m_MainTabPanel;

	public RootMenuType m_RootMenuType;

	[HideInInspector]
	public EditorHack_BaseMenuBehaviour[] m_EditorTabAbleMenuTypes;

	private BaseMenuBehaviour[] m_AllBaseMenuBehaviours;

	private IT17EventHelper[] m_EventHelperInterfaces;

	private int m_CurrentSelectedTab;

	protected bool m_bIsDataInitialized;

	protected override void Awake()
	{
		base.Awake();
		InitializeData();
	}

	public virtual void InitializeData()
	{
		if (m_bIsDataInitialized)
		{
			return;
		}
		m_bIsDataInitialized = true;
		m_AllBaseMenuBehaviours = GetComponentsInChildren<BaseMenuBehaviour>(true);
		m_EventHelperInterfaces = GetComponentsInChildren<IT17EventHelper>(true);
		if (m_AllBaseMenuBehaviours == null)
		{
			return;
		}
		for (int i = 0; i < m_AllBaseMenuBehaviours.Length; i++)
		{
			if (m_AllBaseMenuBehaviours[i] != this)
			{
				m_AllBaseMenuBehaviours[i].DoSingleTimeInitialize();
			}
		}
	}

	protected override void Start()
	{
		base.Start();
	}

	public virtual BaseMenuBehaviour GetMenuOfType<T>()
	{
		return null;
	}

	public virtual int GetTabNumberOfType<T>()
	{
		return -1;
	}

	public virtual BaseMenuBehaviour GetCurrentOpenMenu()
	{
		return null;
	}

	public virtual List<BaseMenuBehaviour> GetCurrentMenuSet()
	{
		return null;
	}

	public override bool Show(GamepadUser currentGamer, BaseMenuBehaviour parent, GameObject invoker, bool hideInvoker = true)
	{
		if (!base.Show(currentGamer, parent, invoker, hideInvoker))
		{
			return false;
		}
		if (m_EventHelperInterfaces != null)
		{
			for (int i = 0; i < m_EventHelperInterfaces.Length; i++)
			{
				if (m_EventHelperInterfaces[i] != null && currentGamer != null)
				{
					m_EventHelperInterfaces[i].SetEventSystem(m_CachedEventSystem);
				}
			}
		}
		return true;
	}

	public override bool Hide(bool restoreInvokerState = true, bool isTabSwitch = false)
	{
		if (!base.Hide(restoreInvokerState, isTabSwitch))
		{
			return false;
		}
		if (m_MainTabPanel != null)
		{
			m_CurrentSelectedTab = m_MainTabPanel.CurrentTabIndex;
		}
		return true;
	}
}
