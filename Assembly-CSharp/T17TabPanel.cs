using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

public class T17TabPanel : BaseMenuBehaviour
{
	public enum RelativePosition
	{
		Top = 0,
		Left = 1,
		Right = 2,
		Bottom = 3
	}

	[HideInInspector]
	public T17Button[] m_Buttons;

	[HideInInspector]
	public BaseMenuBehaviour[] m_MenuBodies;

	[HideInInspector]
	public UnityEvent[] m_MenuDelegates;

	[HideInInspector]
	public bool m_bHasEventsEnabled;

	[HideInInspector]
	public bool m_bKeepSelectedTabHighlighted = true;

	[HideInInspector]
	public bool m_bTabEntriesSetExternally;

	[HideInInspector]
	public Sprite[] m_TabSplitterSprites;

	[HideInInspector]
	public string[] m_TabTitleTags;

	public RelativePosition m_RelativePositionToBodies;

	[HideInInspector]
	public bool m_bAllowDirectNavigation;

	[HideInInspector]
	public bool m_bAllowIndirectNavigation = true;

	[HideInInspector]
	public bool m_bAutoInvokeOnTabSelection;

	[HideInInspector]
	public string m_PreviousTabInputAction = "CycleLeft";

	[HideInInspector]
	public string m_NextTabInputAction = "CycleRight";

	public static readonly int m_ButtonAnimatorHoldBool = Animator.StringToHash("HoldSelected");

	public Color m_TabDisabledColour = Color.black;

	public T17Text m_TabTitle;

	public T17Image m_TabSplitter;

	private int m_OldTabIndex;

	private int m_CurrentTabIndex;

	private int m_MaxTabIndex;

	private ColorBlock m_CurrentButtonNormalColors;

	private ColorBlock m_CurrentButtonHighlightColors;

	private SpriteState m_CurrentButtonSpriteState;

	private Sprite m_CurrentButtonNormalSprite;

	private T17Image[] m_ButtonChildImages;

	public int CurrentTabIndex
	{
		get
		{
			return m_CurrentTabIndex;
		}
	}

	public int PreviousTabIndex
	{
		get
		{
			return m_OldTabIndex;
		}
	}

	protected override void Awake()
	{
		base.Awake();
	}

	private void GetButtonsInDirectChildren()
	{
		int childCount = base.transform.childCount;
		List<T17Button> list = new List<T17Button>();
		for (int i = 0; i < childCount; i++)
		{
			Transform child = base.transform.GetChild(i);
			T17Button component = child.GetComponent<T17Button>();
			if (component != null)
			{
				list.Add(component);
			}
		}
		m_Buttons = list.ToArray();
	}

	protected override void SingleTimeInitialize()
	{
		base.SingleTimeInitialize();
		GetButtonsInDirectChildren();
		m_ButtonChildImages = new T17Image[m_Buttons.Length];
		for (int i = 0; i < m_Buttons.Length; i++)
		{
			T17Button t17Button = m_Buttons[i];
			t17Button.OnButtonSelect = OnTabSelected;
			t17Button.OnButtonPointerEnter = OnTabSelected;
			if (t17Button.transform.childCount > 0)
			{
				m_ButtonChildImages[i] = t17Button.transform.GetChild(0).GetComponent<T17Image>();
			}
		}
		if (m_bTabEntriesSetExternally)
		{
			m_MenuBodies = new BaseMenuBehaviour[m_Buttons.Length];
		}
		else
		{
			m_MaxTabIndex = m_Buttons.Length - 1;
		}
		m_CurrentButtonNormalColors = m_Buttons[m_CurrentTabIndex].colors;
		m_CurrentButtonHighlightColors = m_Buttons[m_CurrentTabIndex].colors;
		m_CurrentButtonSpriteState = m_Buttons[m_CurrentTabIndex].spriteState;
		m_CurrentButtonNormalSprite = ((T17Image)m_Buttons[m_CurrentTabIndex].targetGraphic).sprite;
		for (int j = 0; j < m_MenuBodies.Length; j++)
		{
			if (m_MenuBodies[j] != null)
			{
				m_MenuBodies[j].Hide();
			}
		}
		SetTabIndex(m_CurrentTabIndex);
	}

	protected override void Update()
	{
		base.Update();
	}

	public void SetMenuBodies(List<BaseMenuBehaviour> menus)
	{
		if (menus == null || menus.Count <= 0)
		{
			return;
		}
		for (int i = 0; i < m_Buttons.Length; i++)
		{
			if (m_Buttons[i] != null)
			{
				m_Buttons[i].gameObject.SetActive(false);
				Animator component = m_Buttons[i].GetComponent<Animator>();
				if (component != null)
				{
					component.SetBool(m_ButtonAnimatorHoldBool, false);
					component.SetTrigger(m_Buttons[m_CurrentTabIndex].animationTriggers.normalTrigger);
				}
			}
		}
		for (int j = 0; j < m_MenuBodies.Length; j++)
		{
			if (m_MenuBodies[j] != null)
			{
				m_MenuBodies[j].Hide(true, true);
			}
		}
		for (int k = 0; k < menus.Count; k++)
		{
			if (k < m_Buttons.Length)
			{
				m_MenuBodies[k] = menus[k];
				if (m_Buttons[k] != null)
				{
					m_Buttons[k].gameObject.SetActive(true);
					SetButtonInteractableState(k);
				}
			}
		}
		m_MaxTabIndex = m_Buttons.Length - 1;
	}

	private void SetButtonInteractableState(int index)
	{
		if (m_Buttons == null || m_MenuBodies == null || index < 0 || index >= m_Buttons.Length || index >= m_MenuBodies.Length || m_Buttons[index] == null || m_MenuBodies[index] == null)
		{
			return;
		}
		if (m_Buttons[index].m_lockImage != null)
		{
			m_Buttons[index].m_lockImage.gameObject.SetActive(!m_MenuBodies[index].m_bMenuIsEnabled);
		}
		m_Buttons[index].interactable = m_MenuBodies[index].m_bMenuIsEnabled;
		if (m_ButtonChildImages != null && index <= m_ButtonChildImages.Length && m_ButtonChildImages[index] != null)
		{
			if (m_MenuBodies[index].m_bMenuIsEnabled)
			{
				m_ButtonChildImages[index].sprite = m_MenuBodies[index].m_TabIcon;
			}
			else
			{
				m_ButtonChildImages[index].sprite = m_MenuBodies[index].m_TabIconDisabled;
			}
		}
	}

	public void SetTabIndex(int index, Action<bool> onCompleted = null)
	{
		if (index >= 0 && index < m_Buttons.Length)
		{
			OnTabButtonClicked(m_Buttons[index], onCompleted);
		}
	}

	public void SetMenuEnabled(int index, bool enabled)
	{
		if (m_MenuBodies != null && index >= 0 && index < m_MenuBodies.Length && m_MenuBodies[index] != null)
		{
			m_MenuBodies[index].m_bMenuIsEnabled = enabled;
			SetButtonInteractableState(index);
		}
	}

	public bool CheckMenuEnabled(int index)
	{
		if (m_MenuBodies != null && index >= 0 && index < m_MenuBodies.Length)
		{
			return m_MenuBodies[index].m_bMenuIsEnabled;
		}
		return false;
	}

	public void OnTabSelected(T17Button button)
	{
		if (!button.interactable)
		{
			return;
		}
		if (m_MenuBodies == null || m_CurrentTabIndex >= m_MenuBodies.Length || m_MenuBodies[m_CurrentTabIndex] == null)
		{
			_OnTabButtonSelected(button);
			return;
		}
		m_MenuBodies[m_CurrentTabIndex].ConfirmChangeFocus(delegate(bool canChangeFocus)
		{
			if (canChangeFocus)
			{
				_OnTabButtonSelected(button);
			}
		});
	}

	public void OnTabButtonClicked(T17Button button, Action<bool> onCompleted = null)
	{
		if (m_MenuBodies == null || m_CurrentTabIndex >= m_MenuBodies.Length || m_MenuBodies[m_CurrentTabIndex] == null)
		{
			_OnTabButtonSelected(button);
			if (onCompleted != null)
			{
				onCompleted(true);
			}
			return;
		}
		m_MenuBodies[m_CurrentTabIndex].ConfirmChangeFocus(delegate(bool canChangeFocus)
		{
			if (canChangeFocus)
			{
				_OnTabButtonSelected(button);
				if (onCompleted != null)
				{
					onCompleted(true);
				}
			}
			else if (onCompleted != null)
			{
				onCompleted(false);
			}
		});
	}

	private void _OnTabButtonSelected(T17Button button)
	{
		T17FrontendFlow instance = T17FrontendFlow.Instance;
		if (instance != null)
		{
			instance.FocusOnMainMenu();
		}
		int i;
		for (i = 0; i < m_Buttons.Length && !(m_Buttons[i] == button); i++)
		{
		}
		T17Button t17Button = m_Buttons[m_CurrentTabIndex];
		Animator animator = t17Button.animator;
		if (animator != null)
		{
			animator.SetBool(m_ButtonAnimatorHoldBool, false);
			animator.ResetTrigger(t17Button.animationTriggers.highlightedTrigger);
		}
		if (m_CachedEventSystem != null && m_CachedEventSystem.currentSelectedGameObject != button.gameObject)
		{
			m_CachedEventSystem.SetSelectedGameObject(null);
			m_CachedEventSystem.SetSelectedGameObject(button.gameObject);
		}
		if (m_bHasEventsEnabled && i < m_MenuDelegates.Length && m_MenuDelegates[i] != null)
		{
			m_MenuDelegates[i].Invoke();
		}
		SwitchToTab(i);
		if (m_bKeepSelectedTabHighlighted)
		{
			if (m_Buttons[m_OldTabIndex].transition == Selectable.Transition.ColorTint)
			{
				m_Buttons[m_OldTabIndex].colors = m_CurrentButtonNormalColors;
			}
			else if (m_Buttons[m_OldTabIndex].transition == Selectable.Transition.SpriteSwap)
			{
				((T17Image)m_Buttons[m_OldTabIndex].targetGraphic).sprite = m_CurrentButtonNormalSprite;
			}
			if (button.transition == Selectable.Transition.ColorTint)
			{
				m_CurrentButtonNormalColors = button.colors;
				m_CurrentButtonHighlightColors = button.colors;
				m_CurrentButtonHighlightColors.normalColor = m_CurrentButtonHighlightColors.highlightedColor;
				m_CurrentButtonHighlightColors.highlightedColor = Color.cyan;
				m_Buttons[m_CurrentTabIndex].colors = m_CurrentButtonHighlightColors;
			}
			else if (button.transition == Selectable.Transition.SpriteSwap)
			{
				m_CurrentButtonSpriteState = button.spriteState;
				m_CurrentButtonNormalSprite = ((T17Image)button.targetGraphic).sprite;
				((T17Image)button.targetGraphic).sprite = m_CurrentButtonSpriteState.pressedSprite;
			}
			else if (button.transition == Selectable.Transition.Animation)
			{
				button.animator.SetBool(m_ButtonAnimatorHoldBool, true);
				m_Buttons[m_CurrentTabIndex].animator.SetTrigger(m_Buttons[m_CurrentTabIndex].animationTriggers.highlightedTrigger);
				m_Buttons[m_CurrentTabIndex].animator.ResetTrigger(m_Buttons[m_CurrentTabIndex].animationTriggers.normalTrigger);
			}
		}
	}

	private void SwitchToTab(int tabIndex)
	{
		if (m_MenuBodies[m_CurrentTabIndex] != null && m_CurrentTabIndex != tabIndex)
		{
			m_MenuBodies[m_CurrentTabIndex].Hide(true, true);
		}
		if (tabIndex < m_MenuBodies.Length && m_MenuBodies[tabIndex] != null)
		{
			m_OldTabIndex = m_CurrentTabIndex;
			m_CurrentTabIndex = tabIndex;
			if (m_MenuBodies[m_CurrentTabIndex] != null)
			{
				m_MenuBodies[m_CurrentTabIndex].Show(m_CurrentGamepadUser, this, null);
				SetNavigationLinking(false);
			}
		}
		else
		{
			m_OldTabIndex = m_CurrentTabIndex;
			m_CurrentTabIndex = tabIndex;
		}
		if (m_TabSplitterSprites != null && m_TabSplitter != null && m_CurrentTabIndex < m_TabSplitterSprites.Length)
		{
			m_TabSplitter.sprite = m_TabSplitterSprites[m_CurrentTabIndex];
		}
		if (m_TabTitleTags != null && m_TabTitle != null && m_CurrentTabIndex < m_TabTitleTags.Length)
		{
			m_TabTitle.m_PlaceholderText = m_TabTitleTags[m_CurrentTabIndex];
			m_TabTitle.SetNewLocalizationTag(m_TabTitleTags[m_CurrentTabIndex]);
		}
		IMenuEventDelegate componentInParent = GetComponentInParent<IMenuEventDelegate>();
		if (componentInParent != null)
		{
			componentInParent.ChildMenuChanged();
		}
	}

	private void SetNavigationLinking(bool selectFirstItemOnBody)
	{
		if (m_MenuBodies == null || m_CurrentTabIndex >= m_MenuBodies.Length || m_MenuBodies[m_CurrentTabIndex] == null)
		{
			return;
		}
		GameObject selectedGameObject = null;
		switch (m_RelativePositionToBodies)
		{
		case RelativePosition.Left:
			if (m_MenuBodies[m_CurrentTabIndex].m_BorderSelectables.selectOnLeft != null)
			{
				if (m_bAllowDirectNavigation)
				{
					Navigation navigation = m_MenuBodies[m_CurrentTabIndex].m_BorderSelectables.selectOnLeft.navigation;
					navigation.selectOnLeft = m_Buttons[m_CurrentTabIndex];
					m_MenuBodies[m_CurrentTabIndex].m_BorderSelectables.selectOnLeft.navigation = navigation;
					navigation = m_Buttons[m_CurrentTabIndex].navigation;
					navigation.selectOnRight = m_MenuBodies[m_CurrentTabIndex].m_BorderSelectables.selectOnLeft;
					m_Buttons[m_CurrentTabIndex].navigation = navigation;
				}
				selectedGameObject = m_MenuBodies[m_CurrentTabIndex].m_BorderSelectables.selectOnLeft.gameObject;
			}
			break;
		case RelativePosition.Right:
			if (m_MenuBodies[m_CurrentTabIndex].m_BorderSelectables.selectOnRight != null)
			{
				if (m_bAllowDirectNavigation)
				{
					Navigation navigation = m_MenuBodies[m_CurrentTabIndex].m_BorderSelectables.selectOnRight.navigation;
					navigation.selectOnRight = m_Buttons[m_CurrentTabIndex];
					m_MenuBodies[m_CurrentTabIndex].m_BorderSelectables.selectOnRight.navigation = navigation;
					navigation = m_Buttons[m_CurrentTabIndex].navigation;
					navigation.selectOnLeft = m_MenuBodies[m_CurrentTabIndex].m_BorderSelectables.selectOnRight;
					m_Buttons[m_CurrentTabIndex].navigation = navigation;
				}
				selectedGameObject = m_MenuBodies[m_CurrentTabIndex].m_BorderSelectables.selectOnRight.gameObject;
			}
			break;
		case RelativePosition.Bottom:
			if (m_MenuBodies[m_CurrentTabIndex].m_BorderSelectables.selectOnDown != null)
			{
				if (m_bAllowDirectNavigation)
				{
					Navigation navigation = m_MenuBodies[m_CurrentTabIndex].m_BorderSelectables.selectOnDown.navigation;
					navigation.selectOnDown = m_Buttons[m_CurrentTabIndex];
					m_MenuBodies[m_CurrentTabIndex].m_BorderSelectables.selectOnDown.navigation = navigation;
					navigation = m_Buttons[m_CurrentTabIndex].navigation;
					navigation.selectOnUp = m_MenuBodies[m_CurrentTabIndex].m_BorderSelectables.selectOnDown;
					m_Buttons[m_CurrentTabIndex].navigation = navigation;
				}
				selectedGameObject = m_MenuBodies[m_CurrentTabIndex].m_BorderSelectables.selectOnDown.gameObject;
			}
			break;
		default:
			if (m_MenuBodies[m_CurrentTabIndex].m_BorderSelectables.selectOnUp != null)
			{
				if (m_bAllowDirectNavigation)
				{
					Navigation navigation = m_MenuBodies[m_CurrentTabIndex].m_BorderSelectables.selectOnUp.navigation;
					navigation.selectOnUp = m_Buttons[m_CurrentTabIndex];
					m_MenuBodies[m_CurrentTabIndex].m_BorderSelectables.selectOnUp.navigation = navigation;
					navigation = m_Buttons[m_CurrentTabIndex].navigation;
					navigation.selectOnDown = m_MenuBodies[m_CurrentTabIndex].m_BorderSelectables.selectOnUp;
					m_Buttons[m_CurrentTabIndex].navigation = navigation;
				}
				selectedGameObject = m_MenuBodies[m_CurrentTabIndex].m_BorderSelectables.selectOnUp.gameObject;
			}
			break;
		}
		if ((selectFirstItemOnBody || !m_bAllowDirectNavigation) && m_CachedEventSystem != null && m_Buttons[m_CurrentTabIndex] != null)
		{
			m_CachedEventSystem.SetSelectedGameObject(null);
			m_CachedEventSystem.SetSelectedGameObject(selectedGameObject);
		}
	}

	private void OnEnable()
	{
		if (EventSystem.current != null && m_Buttons[m_CurrentTabIndex] != null && m_Buttons[m_CurrentTabIndex].transition == Selectable.Transition.Animation)
		{
			if (m_bKeepSelectedTabHighlighted)
			{
				m_Buttons[m_CurrentTabIndex].animator.SetBool(m_ButtonAnimatorHoldBool, true);
			}
			m_Buttons[m_CurrentTabIndex].animator.SetTrigger(m_Buttons[m_CurrentTabIndex].animationTriggers.highlightedTrigger);
		}
	}

	public BaseMenuBehaviour GetCurrentPage()
	{
		if (m_MenuBodies == null || m_CurrentTabIndex >= m_MenuBodies.Length || m_MenuBodies[m_CurrentTabIndex] == null)
		{
			return null;
		}
		return m_MenuBodies[m_CurrentTabIndex];
	}

	public void AttemptToSetTabIndex(int index, Action<bool> onCompleted = null)
	{
		int num = index;
		if (!CheckMenuEnabled(num))
		{
			int num2 = m_MenuBodies.Length;
			bool flag = true;
			do
			{
				if (num <= 0)
				{
					flag = false;
					num = index;
				}
				num += ((!flag) ? 1 : (-1));
				num2--;
			}
			while (!CheckMenuEnabled(num) && num2 > 0);
		}
		if (CheckMenuEnabled(num))
		{
			SetTabIndex(num, onCompleted);
		}
	}

	public void SelectPreviousTab()
	{
		int num = m_CurrentTabIndex - 1;
		if (num < 0)
		{
			num = 0;
		}
		if (num != m_CurrentTabIndex)
		{
			SetTabIndex(num);
		}
	}

	public void SelectNextTab()
	{
		int num = m_CurrentTabIndex + 1;
		if (num > m_MaxTabIndex)
		{
			num = m_MaxTabIndex;
		}
		if (num != m_CurrentTabIndex)
		{
			SetTabIndex(num);
		}
	}

	public void CollapseCurrentTab()
	{
		if (m_CurrentTabIndex < m_MenuBodies.Length && m_Buttons[m_CurrentTabIndex] != null)
		{
			m_MenuBodies[m_CurrentTabIndex].Hide(false);
			if (m_Buttons[m_CurrentTabIndex].animator != null)
			{
				m_Buttons[m_CurrentTabIndex].animator.SetBool(m_ButtonAnimatorHoldBool, false);
				m_Buttons[m_CurrentTabIndex].animator.SetTrigger(m_Buttons[m_CurrentTabIndex].animationTriggers.normalTrigger);
			}
		}
	}

	public void ExpandCurrentTab()
	{
		AttemptToSetTabIndex(m_CurrentTabIndex);
	}
}
