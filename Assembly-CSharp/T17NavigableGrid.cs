using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class T17NavigableGrid : FrontendMenuBehaviour
{
	private struct LayoutSize
	{
		public int x;

		public int y;
	}

	public RectTransform m_ContentParent;

	public bool m_ContentIsDynamic;

	protected List<RectTransform> m_ContentSelectables = new List<RectTransform>();

	protected int m_PreviousSelected;

	protected int m_CurrentSelected;

	private bool m_bAltElementLayout;

	private bool m_AltLayoutIsVertical;

	private LayoutSize m_AltLayoutSize = default(LayoutSize);

	private bool m_bContentElementSelected;

	private Selectable m_DataCache;

	protected bool isAltLayout
	{
		get
		{
			return m_bAltElementLayout;
		}
	}

	protected override void SingleTimeInitialize()
	{
		base.SingleTimeInitialize();
		if (m_ContentParent != null)
		{
			T17GridLayoutGroup component = m_ContentParent.GetComponent<T17GridLayoutGroup>();
			if (component != null)
			{
				m_AltLayoutSize.x = component.m_CellCountX;
				m_AltLayoutSize.y = component.m_CellCountY;
				m_AltLayoutIsVertical = component.startAxis == T17GridLayoutGroup.Axis.Vertical;
				m_bAltElementLayout = true;
			}
			if (!m_ContentIsDynamic)
			{
				for (int i = 0; i < m_ContentParent.transform.childCount; i++)
				{
					AddNewObject(m_ContentParent.transform.GetChild(i).gameObject);
				}
			}
			if (component != null)
			{
				component.ForceRefresh();
			}
		}
		if (m_BorderSelectables.selectOnUp != null)
		{
			T17Image component2 = m_BorderSelectables.selectOnUp.GetComponent<T17Image>();
			if (component2 != null)
			{
				component2.enabled = false;
			}
		}
		if (m_BorderSelectables.selectOnLeft != null)
		{
			T17Image component3 = m_BorderSelectables.selectOnLeft.GetComponent<T17Image>();
			if (component3 != null)
			{
				component3.enabled = false;
			}
		}
		if (m_BorderSelectables.selectOnRight != null)
		{
			T17Image component4 = m_BorderSelectables.selectOnRight.GetComponent<T17Image>();
			if (component4 != null)
			{
				component4.enabled = false;
			}
		}
		if (m_BorderSelectables.selectOnDown != null)
		{
			T17Image component5 = m_BorderSelectables.selectOnDown.GetComponent<T17Image>();
			if (component5 != null)
			{
				component5.enabled = false;
			}
		}
	}

	public override bool Show(GamepadUser currentGamer, BaseMenuBehaviour parent, GameObject invoker, bool hideInvoker = true)
	{
		bool result = base.Show(currentGamer, parent, invoker, hideInvoker);
		if (m_ContentIsDynamic)
		{
			if (m_ContentParent != null)
			{
				T17GridLayoutGroup component = m_ContentParent.GetComponent<T17GridLayoutGroup>();
				if (component != null)
				{
					m_AltLayoutSize.x = component.m_CellCountX;
					m_AltLayoutSize.y = component.m_CellCountY;
				}
			}
			m_ContentSelectables.Clear();
			for (int i = 0; i < m_ContentParent.transform.childCount; i++)
			{
				GameObject gameObject = m_ContentParent.transform.GetChild(i).gameObject;
				if (gameObject.activeSelf)
				{
					AddNewObject(gameObject);
				}
			}
			if (m_bAltElementLayout)
			{
				T17GridLayoutGroup component2 = m_ContentParent.GetComponent<T17GridLayoutGroup>();
				if (component2 != null)
				{
					component2.ForceRefresh();
				}
			}
		}
		return result;
	}

	public override bool Hide(bool restoreInvokerState = true, bool isTabSwitch = false)
	{
		bool result = base.Hide(restoreInvokerState, isTabSwitch);
		if (m_ContentIsDynamic)
		{
			m_ContentSelectables.Clear();
		}
		return result;
	}

	protected override void Update()
	{
		base.Update();
		if (m_DataCache != null)
		{
			if (m_CachedEventSystem == null)
			{
				m_DataCache = null;
				return;
			}
			GameObject lastRequestedSelectedGameobject = m_CachedEventSystem.GetLastRequestedSelectedGameobject();
			m_CachedEventSystem.SetSelectedGameObject(null);
			if (m_DataCache == m_BorderSelectables.selectOnUp)
			{
				if (m_bContentElementSelected && m_BorderSelectables.selectOnUp.navigation.selectOnUp != null)
				{
					m_bContentElementSelected = false;
					m_CachedEventSystem.SetSelectedGameObject(m_BorderSelectables.selectOnUp.navigation.selectOnUp.gameObject);
				}
				else if (!ReselectCurrent(ref m_CachedEventSystem))
				{
					GameObject gameObject = ((!(m_BorderSelectables.selectOnUp.navigation.selectOnUp == null) && m_BorderSelectables.selectOnUp.navigation.selectOnUp.IsInteractable()) ? m_BorderSelectables.selectOnUp.navigation.selectOnUp.gameObject : null);
					if (gameObject == null && lastRequestedSelectedGameobject != null)
					{
						m_CachedEventSystem.SetSelectedGameObject(lastRequestedSelectedGameobject);
					}
					else
					{
						m_CachedEventSystem.SetSelectedGameObject(gameObject);
					}
				}
			}
			else if (m_DataCache == m_BorderSelectables.selectOnDown)
			{
				if (m_bContentElementSelected && m_BorderSelectables.selectOnDown.navigation.selectOnDown != null)
				{
					m_bContentElementSelected = false;
					m_CachedEventSystem.SetSelectedGameObject(m_BorderSelectables.selectOnDown.navigation.selectOnDown.gameObject);
				}
				else if (!ReselectCurrent(ref m_CachedEventSystem))
				{
					GameObject selectedGameObject = ((!(m_BorderSelectables.selectOnDown.navigation.selectOnDown == null) && m_BorderSelectables.selectOnDown.navigation.selectOnDown.IsInteractable()) ? m_BorderSelectables.selectOnDown.navigation.selectOnDown.gameObject : null);
					m_CachedEventSystem.SetSelectedGameObject(selectedGameObject);
				}
			}
			else if (m_DataCache == m_BorderSelectables.selectOnLeft)
			{
				if (m_bContentElementSelected && m_BorderSelectables.selectOnLeft.navigation.selectOnLeft != null)
				{
					m_bContentElementSelected = false;
					m_CachedEventSystem.SetSelectedGameObject(m_BorderSelectables.selectOnLeft.navigation.selectOnLeft.gameObject);
				}
				else if (!ReselectCurrent(ref m_CachedEventSystem))
				{
					GameObject selectedGameObject2 = ((!(m_BorderSelectables.selectOnLeft.navigation.selectOnLeft == null) && m_BorderSelectables.selectOnLeft.navigation.selectOnLeft.IsInteractable()) ? m_BorderSelectables.selectOnLeft.navigation.selectOnLeft.gameObject : null);
					m_CachedEventSystem.SetSelectedGameObject(selectedGameObject2);
				}
			}
			else if (m_DataCache == m_BorderSelectables.selectOnRight)
			{
				if (m_bContentElementSelected && m_BorderSelectables.selectOnRight.navigation.selectOnRight != null)
				{
					m_bContentElementSelected = false;
					m_CachedEventSystem.SetSelectedGameObject(m_BorderSelectables.selectOnRight.navigation.selectOnRight.gameObject);
				}
				else if (!ReselectCurrent(ref m_CachedEventSystem))
				{
					GameObject selectedGameObject3 = ((!(m_BorderSelectables.selectOnRight.navigation.selectOnRight == null) && m_BorderSelectables.selectOnRight.navigation.selectOnRight.IsInteractable()) ? m_BorderSelectables.selectOnRight.navigation.selectOnRight.gameObject : null);
					m_CachedEventSystem.SetSelectedGameObject(selectedGameObject3);
				}
			}
		}
		m_DataCache = null;
	}

	public virtual void AddNewObject(GameObject newObject)
	{
		if (newObject == null)
		{
			return;
		}
		RectTransform component = newObject.GetComponent<RectTransform>();
		if (m_ContentSelectables.Contains(component))
		{
			return;
		}
		m_ContentSelectables.Add(component);
		newObject.transform.SetParent(m_ContentParent.transform);
		newObject.transform.localScale = Vector3.one;
		newObject.transform.localPosition = Vector3.zero;
		int currentIndex = m_ContentSelectables.Count - 1;
		Selectable sel = newObject.GetComponentInChildren<Selectable>(true);
		T17_UISelectDeselectEvents t17_UISelectDeselectEvents = newObject.GetComponentInChildren<T17_UISelectDeselectEvents>(true);
		if (sel == null)
		{
			sel = newObject.AddComponent<Selectable>();
		}
		if (t17_UISelectDeselectEvents == null)
		{
			t17_UISelectDeselectEvents = newObject.AddComponent<T17_UISelectDeselectEvents>();
		}
		if (t17_UISelectDeselectEvents != null)
		{
			t17_UISelectDeselectEvents.m_OnSelectEvent.AddListener(delegate
			{
				OnElementSelected(sel, currentIndex);
			});
		}
		Navigation navigation = sel.navigation;
		navigation.mode = Navigation.Mode.Explicit;
		if (!m_bAltElementLayout)
		{
			if (currentIndex == 0)
			{
				navigation.selectOnUp = m_BorderSelectables.selectOnUp;
			}
			else
			{
				Selectable component2 = m_ContentSelectables[currentIndex - 1].GetComponent<Selectable>();
				Navigation navigation2 = component2.navigation;
				navigation.selectOnUp = component2;
				navigation2.selectOnDown = sel;
				component2.navigation = navigation2;
			}
			navigation.selectOnLeft = m_BorderSelectables.selectOnLeft;
			navigation.selectOnRight = m_BorderSelectables.selectOnRight;
			navigation.selectOnDown = m_BorderSelectables.selectOnDown;
		}
		else
		{
			if (currentIndex == 0)
			{
				navigation.selectOnUp = m_BorderSelectables.selectOnUp;
				navigation.selectOnLeft = m_BorderSelectables.selectOnLeft;
			}
			else
			{
				RectTransform rectTransform = null;
				if (m_AltLayoutIsVertical)
				{
					if (currentIndex % m_AltLayoutSize.y > 0)
					{
						rectTransform = m_ContentSelectables[currentIndex - 1];
					}
				}
				else
				{
					int num = currentIndex - m_AltLayoutSize.x;
					if (num >= 0)
					{
						rectTransform = m_ContentSelectables[num];
					}
				}
				RectTransform rectTransform2 = null;
				if (m_AltLayoutIsVertical)
				{
					int num2 = currentIndex - m_AltLayoutSize.y;
					if (num2 >= 0)
					{
						rectTransform2 = m_ContentSelectables[num2];
					}
				}
				else if (m_AltLayoutSize.x != 0 && currentIndex % m_AltLayoutSize.x > 0)
				{
					rectTransform2 = m_ContentSelectables[currentIndex - 1];
				}
				if (rectTransform != null)
				{
					Selectable selectable = (navigation.selectOnUp = rectTransform.GetComponent<Selectable>());
					Navigation navigation3 = selectable.navigation;
					navigation3.selectOnDown = sel;
					selectable.navigation = navigation3;
				}
				else
				{
					navigation.selectOnUp = m_BorderSelectables.selectOnUp;
				}
				if (rectTransform2 != null)
				{
					Selectable selectable2 = (navigation.selectOnLeft = rectTransform2.GetComponent<Selectable>());
					Navigation navigation4 = selectable2.navigation;
					navigation4.selectOnRight = sel;
					selectable2.navigation = navigation4;
				}
				else
				{
					navigation.selectOnLeft = m_BorderSelectables.selectOnLeft;
				}
			}
			navigation.selectOnRight = m_BorderSelectables.selectOnRight;
			navigation.selectOnDown = m_BorderSelectables.selectOnDown;
		}
		sel.navigation = navigation;
		Navigation navigation5 = m_BorderSelectables.selectOnDown.navigation;
		navigation5.selectOnUp = sel;
		m_BorderSelectables.selectOnDown.navigation = navigation5;
	}

	public virtual void SelectFirstElement()
	{
		if (m_ContentSelectables != null && m_ContentSelectables.Count > 0 && m_CachedEventSystem != null)
		{
			m_CachedEventSystem.SetSelectedGameObject(null);
			m_CachedEventSystem.SetSelectedGameObject(m_ContentSelectables[0].gameObject);
		}
	}

	protected virtual bool ReselectCurrent(ref T17EventSystem m_CachedEventSystem)
	{
		if (m_ContentSelectables != null && m_ContentSelectables.Count > 0)
		{
			if (m_CurrentSelected >= 0 && m_CurrentSelected < m_ContentSelectables.Count)
			{
				m_CachedEventSystem.SetSelectedGameObject(m_ContentSelectables[m_CurrentSelected].gameObject);
				return true;
			}
			if (m_ContentSelectables[0] != null)
			{
				m_CachedEventSystem.SetSelectedGameObject(m_ContentSelectables[0].gameObject);
				return true;
			}
		}
		return false;
	}

	protected virtual void OnElementSelected(Selectable sel, int index)
	{
		m_bContentElementSelected = true;
		m_PreviousSelected = m_CurrentSelected;
		m_CurrentSelected = index;
	}

	public void RedirectEdgeLink(Selectable data)
	{
		m_DataCache = data;
	}

	public void ClearContents()
	{
		for (int num = m_ContentSelectables.Count - 1; num >= 0; num--)
		{
			RectTransform rectTransform = m_ContentSelectables[num];
			if (rectTransform != null && rectTransform.gameObject != null)
			{
				rectTransform.gameObject.SetActive(false);
				Object.Destroy(rectTransform.gameObject);
			}
		}
		m_ContentSelectables.Clear();
	}
}
