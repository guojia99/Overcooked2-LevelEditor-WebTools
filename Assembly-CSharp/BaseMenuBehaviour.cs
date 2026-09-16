using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class BaseMenuBehaviour : MonoBehaviour, IMenuEventDelegate
{
	public delegate void BaseMenuBehaviourEvent(BaseMenuBehaviour menu);

	public delegate void ConfirmFocusCallback(bool canChangeFocus);

	protected T17EventSystem m_CachedEventSystem;

	protected GamepadUser m_CurrentGamepadUser;

	[HideInInspector]
	public Navigation m_BorderSelectables;

	[HideInInspector]
	public Sprite m_TabIcon;

	[HideInInspector]
	public Sprite m_TabIconDisabled;

	public string m_legendText;

	public string m_legendTextClient;

	public string m_legendTextInSession;

	public Animator m_TransitionAnimator;

	public string m_BackTransition = "TransitionBack";

	public string m_ForwardTransition = "TransitionForward";

	public bool m_bMenuIsEnabled = true;

	public BaseMenuBehaviourEvent OnHide;

	public BaseMenuBehaviourEvent OnShow;

	protected HashSet<Selectable> m_AllowedSelectables = new HashSet<Selectable>();

	protected List<BaseMenuBehaviour> m_ChildMenus;

	protected NavigateOnUICancel m_NavigateOnUICancel;

	protected BaseMenuBehaviour m_Parent;

	protected GameObject m_ObjectThatInvokedShow;

	protected bool m_bWasInvokerActive = true;

	protected bool m_bLinkingUpdateRequested;

	protected int m_Waitframe = 1;

	[HideInInspector]
	public bool m_bShouldBlockParentCancelHandler = true;

	private bool m_bAllowCancelHandeling;

	private bool m_bDidSingleTimeInitialize;

	private bool m_bShownThisFrame;

	public static int AllowedCancelHandlers;

	public static BaseMenuBehaviour LastMenuThatCalledShow;

	public T17EventSystem CachedEventSystem
	{
		get
		{
			return m_CachedEventSystem;
		}
	}

	public GamepadUser CurrentGamepadUser
	{
		get
		{
			return m_CurrentGamepadUser;
		}
	}

	public NavigateOnUICancel NavigateOnUICancel
	{
		get
		{
			return m_NavigateOnUICancel;
		}
	}

	public event MenuChangedHandler MenuChangedEvent;

	protected virtual void Awake()
	{
	}

	protected virtual void OnDestroy()
	{
		if (LastMenuThatCalledShow == this)
		{
			LastMenuThatCalledShow = null;
		}
	}

	protected virtual void Start()
	{
		if (!m_bDidSingleTimeInitialize)
		{
			SingleTimeInitialize();
		}
	}

	protected virtual void Update()
	{
		if (m_bLinkingUpdateRequested)
		{
			if (m_Waitframe <= 0)
			{
				m_bLinkingUpdateRequested = false;
				UpdateAndConstrainSelectableNavigation();
				m_Waitframe = 1;
			}
			else
			{
				m_Waitframe--;
			}
		}
		m_bShownThisFrame = false;
	}

	public void DoSingleTimeInitialize()
	{
		if (!m_bDidSingleTimeInitialize)
		{
			SingleTimeInitialize();
		}
	}

	protected virtual void SingleTimeInitialize()
	{
		m_bDidSingleTimeInitialize = true;
		m_NavigateOnUICancel = GetComponent<NavigateOnUICancel>();
		if (m_TransitionAnimator == null)
		{
			m_TransitionAnimator = GetComponent<Animator>();
		}
	}

	public virtual bool Show(GamepadUser gamepadUser, BaseMenuBehaviour parent, GameObject invoker, bool hideInvoker = true)
	{
		m_CurrentGamepadUser = gamepadUser;
		m_Parent = parent;
		if (m_CurrentGamepadUser == null)
		{
			m_CachedEventSystem = null;
		}
		else
		{
			m_CachedEventSystem = T17EventSystemsManager.Instance.GetEventSystemForGamepadUser(m_CurrentGamepadUser);
		}
		m_ObjectThatInvokedShow = invoker;
		if (!m_bDidSingleTimeInitialize)
		{
			SingleTimeInitialize();
		}
		if (m_bShouldBlockParentCancelHandler && m_NavigateOnUICancel != null)
		{
			m_bAllowCancelHandeling = true;
			AllowedCancelHandlers++;
		}
		if (m_ObjectThatInvokedShow != null)
		{
			m_bWasInvokerActive = m_ObjectThatInvokedShow.activeSelf;
		}
		if (parent != null)
		{
			parent.AddChildMenu(this);
			if (m_bShouldBlockParentCancelHandler && parent.m_NavigateOnUICancel != null)
			{
				parent.m_bAllowCancelHandeling = false;
				AllowedCancelHandlers--;
			}
		}
		if (!base.gameObject.activeInHierarchy)
		{
			if (hideInvoker && m_ObjectThatInvokedShow != null)
			{
				m_ObjectThatInvokedShow.SetActive(false);
			}
			base.gameObject.SetActive(true);
			if (this.MenuChangedEvent != null)
			{
				this.MenuChangedEvent();
			}
			if (m_Parent != null)
			{
				m_Parent.ChildMenuChanged(this, this);
			}
			LastMenuThatCalledShow = this;
			if (OnShow != null)
			{
				OnShow(this);
			}
			m_bShownThisFrame = true;
			return true;
		}
		return false;
	}

	public virtual bool Hide(bool restoreInvokerState = true, bool isTabSwitch = false)
	{
		if (base.gameObject.activeSelf || isTabSwitch)
		{
			if (m_NavigateOnUICancel != null)
			{
				m_bAllowCancelHandeling = false;
				AllowedCancelHandlers--;
			}
			if (m_ObjectThatInvokedShow != null && restoreInvokerState)
			{
				m_ObjectThatInvokedShow.SetActive(m_bWasInvokerActive);
			}
			base.gameObject.SetActive(false);
			if (m_ChildMenus != null)
			{
				for (int num = m_ChildMenus.Count - 1; num >= 0; num--)
				{
					m_ChildMenus[num].Hide(restoreInvokerState, isTabSwitch);
				}
			}
			if (OnHide != null)
			{
				OnHide(this);
			}
			m_CurrentGamepadUser = null;
			m_CachedEventSystem = null;
			if (this.MenuChangedEvent != null)
			{
				this.MenuChangedEvent();
			}
			if (m_Parent != null)
			{
				m_Parent.ChildMenuChanged(this, this);
			}
			return true;
		}
		return false;
	}

	protected virtual void AddChildMenu(BaseMenuBehaviour childMenu)
	{
		if (m_ChildMenus == null)
		{
			m_ChildMenus = new List<BaseMenuBehaviour>();
		}
		if (!m_ChildMenus.Contains(childMenu))
		{
			childMenu.OnHide = (BaseMenuBehaviourEvent)Delegate.Combine(childMenu.OnHide, new BaseMenuBehaviourEvent(OnChildHide));
			m_ChildMenus.Add(childMenu);
		}
	}

	private void OnChildHide(BaseMenuBehaviour childMenu)
	{
		if (m_ChildMenus.Contains(childMenu))
		{
			childMenu.OnHide = (BaseMenuBehaviourEvent)Delegate.Remove(childMenu.OnHide, new BaseMenuBehaviourEvent(OnChildHide));
			m_ChildMenus.Remove(childMenu);
			if (childMenu.m_bShouldBlockParentCancelHandler && m_NavigateOnUICancel != null)
			{
				m_bAllowCancelHandeling = true;
				AllowedCancelHandlers++;
			}
			LastMenuThatCalledShow = this;
		}
	}

	public void GetChildMenus(out List<BaseMenuBehaviour> childList)
	{
		childList = m_ChildMenus;
	}

	protected void RaiseMenuChangedEvent()
	{
		if (this.MenuChangedEvent != null)
		{
			this.MenuChangedEvent();
		}
		if (m_Parent != null)
		{
			m_Parent.ChildMenuChanged(this);
		}
	}

	public void ChildMenuChanged(IMenuEventDelegate sender = null, IMenuEventDelegate changedItem = null)
	{
		RaiseMenuChangedEvent();
		if (m_Parent != null)
		{
			m_Parent.ChildMenuChanged(this, changedItem);
		}
	}

	public void PlayForwardTransition()
	{
		PlayTransition(m_ForwardTransition);
	}

	public void PlayBackTransition()
	{
		PlayTransition(m_BackTransition);
	}

	private void PlayTransition(string trigger)
	{
		if (m_TransitionAnimator != null)
		{
			m_TransitionAnimator.SetTrigger(trigger);
		}
	}

	public virtual void ConfirmChangeFocus(ConfirmFocusCallback confirmCallback)
	{
		if (confirmCallback != null)
		{
			confirmCallback(true);
		}
	}

	public void UpdateAndConstrainSelectableNavigation()
	{
		Selectable[] componentsInChildren = GetComponentsInChildren<Selectable>(true);
		SetAllowedSelectables(new HashSet<Selectable>(componentsInChildren));
		foreach (Selectable selectable in componentsInChildren)
		{
			if (selectable.navigation.mode == Navigation.Mode.Explicit)
			{
				Navigation navigation = new Navigation
				{
					mode = Navigation.Mode.Explicit
				};
				Selectable selectable2 = selectable.FindSelectable(selectable.transform.rotation * Vector3.down);
				if (selectable2 != null && m_AllowedSelectables.Contains(selectable2))
				{
					navigation.selectOnDown = selectable2;
				}
				Selectable selectable3 = selectable.FindSelectable(selectable.transform.rotation * Vector3.up);
				if (selectable3 != null && m_AllowedSelectables.Contains(selectable3))
				{
					navigation.selectOnUp = selectable3;
				}
				Selectable selectable4 = selectable.FindSelectable(selectable.transform.rotation * Vector3.left);
				if (selectable4 != null && m_AllowedSelectables.Contains(selectable4))
				{
					navigation.selectOnLeft = selectable4;
				}
				Selectable selectable5 = selectable.FindSelectable(selectable.transform.rotation * Vector3.right);
				if (selectable5 != null && m_AllowedSelectables.Contains(selectable5))
				{
					navigation.selectOnRight = selectable5;
				}
				selectable.navigation = navigation;
			}
		}
	}

	public void SetAllowedSelectables(HashSet<Selectable> selectables)
	{
		m_AllowedSelectables = selectables;
	}

	public void AddAllowedSelectables(HashSet<Selectable> selectables)
	{
		m_AllowedSelectables.UnionWith(selectables);
	}

	public void AddAllowedSelectables(Selectable selectable)
	{
		m_AllowedSelectables.Add(selectable);
	}

	public string GetLegendText()
	{
		if (ConnectionStatus.IsInSession() && !ConnectionStatus.IsHost() && !string.IsNullOrEmpty(m_legendTextClient))
		{
			return m_legendTextClient;
		}
		return m_legendText;
	}

	public bool InvokeNavigateOnUICancel()
	{
		if (!m_bShownThisFrame && m_bAllowCancelHandeling && m_NavigateOnUICancel != null && m_CurrentGamepadUser != null && (m_CachedEventSystem == null || (m_CachedEventSystem != null && !m_CachedEventSystem.IsDisabled())) && !T17DialogBoxManager.HasDialogsForGamer(m_CurrentGamepadUser))
		{
			m_NavigateOnUICancel.m_DoThisOnUICancel.Invoke();
			return true;
		}
		return false;
	}
}
