using System;
using System.Collections.Generic;
using Team17.Online;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class ControlSchemeToggle : MonoBehaviour
{
	[Serializable]
	private class ControlScheme
	{
		public string m_Name = string.Empty;

		public GamepadUser.ControlTypeEnum m_ControlType;

		public bool m_Split;

		public GameObject m_Scheme;
	}

	[Header("PC Input Toggle")]
	[SerializeField]
	private BaseMenuBehaviour m_ParentMenu;

	[SerializeField]
	private T17Button m_LeftButton;

	[SerializeField]
	private T17Button m_RightButton;

	[SerializeField]
	private T17Button m_DefaultsButton;

	[SerializeField]
	private T17Button m_AcceptButton;

	[SerializeField]
	private T17Text m_ToggleText;

	[SerializeField]
	private ControlScheme[] m_ControlSchemes;

	private int m_ControlSchemeIndex;

	private void Awake()
	{
		SetClickListener(m_LeftButton, OnPreviousControlScheme);
		SetClickListener(m_RightButton, OnNextControlScheme);
		ShowAppropriateControlScheme();
	}

	private void OnEnable()
	{
		if (m_ParentMenu != null)
		{
			KeyboardRebindElement[] componentsInChildren = m_ControlSchemes[m_ControlSchemeIndex].m_Scheme.GetComponentsInChildren<KeyboardRebindElement>();
			Selectable selectable = ((componentsInChildren == null || componentsInChildren.Length <= 0) ? null : componentsInChildren[0].GetComponent<Selectable>());
			if (selectable != null && selectable.gameObject.activeInHierarchy)
			{
				m_ParentMenu.m_BorderSelectables.selectOnUp = selectable;
			}
			else if (m_ControlSchemes[m_ControlSchemeIndex].m_ControlType == GamepadUser.ControlTypeEnum.Keyboard)
			{
				m_ParentMenu.m_BorderSelectables.selectOnUp = m_AcceptButton;
			}
			else
			{
				m_ParentMenu.m_BorderSelectables.selectOnUp = m_RightButton;
			}
		}
	}

	public bool IsCurrentSchemeSplit()
	{
		if (m_ControlSchemeIndex >= 0 && m_ControlSchemeIndex < m_ControlSchemes.Length)
		{
			return m_ControlSchemes[m_ControlSchemeIndex].m_Split;
		}
		return false;
	}

	private void ShowAppropriateControlScheme()
	{
		int num = -1;
		FastList<User> users = ClientUserSystem.m_Users;
		if (users != null && users.Count > 0)
		{
			GamepadUser gamepadUser = users._items[0].GamepadUser;
			if (gamepadUser != null)
			{
				GamepadUser.ControlTypeEnum controlType = gamepadUser.ControlType;
				bool flag = users._items[0].PadSide != PadSide.Both;
				for (int i = 0; i < m_ControlSchemes.Length; i++)
				{
					if (m_ControlSchemes[i].m_ControlType == controlType && (num == -1 || m_ControlSchemes[i].m_Split == flag))
					{
						num = i;
					}
				}
			}
		}
		ShowControlScheme((num != -1) ? num : 0);
	}

	private void OnPreviousControlScheme()
	{
		ShowControlScheme((m_ControlSchemeIndex + (m_ControlSchemes.Length - 1)) % m_ControlSchemes.Length);
	}

	private void OnNextControlScheme()
	{
		ShowControlScheme((m_ControlSchemeIndex + 1) % m_ControlSchemes.Length);
	}

	private void ShowControlScheme(int index)
	{
		m_ControlSchemeIndex = index;
		for (int i = 0; i < m_ControlSchemes.Length; i++)
		{
			m_ControlSchemes[i].m_Scheme.SetActive(i == m_ControlSchemeIndex);
		}
		KeyboardRebindElement[] componentsInChildren = m_ControlSchemes[m_ControlSchemeIndex].m_Scheme.GetComponentsInChildren<KeyboardRebindElement>();
		if (componentsInChildren.Length > 0)
		{
			Selectable component = componentsInChildren[0].GetComponent<Selectable>();
			SetNavigationOnDown(m_LeftButton, component);
			SetNavigationOnDown(m_RightButton, component);
			Selectable component2 = componentsInChildren[componentsInChildren.Length - 1].GetComponent<Selectable>();
			SetNavigationOnUp(m_AcceptButton, component2);
			if (m_ControlSchemes[m_ControlSchemeIndex].m_Split)
			{
				Selectable component3 = componentsInChildren[(componentsInChildren.Length - 1) / 2].GetComponent<Selectable>();
				SetNavigationOnUp(m_DefaultsButton, component3);
				SetNavigationOnDown(component3, m_DefaultsButton);
				SetNavigationOnDown(component2, m_AcceptButton);
			}
			else
			{
				SetNavigationOnUp(m_DefaultsButton, component2);
				SetNavigationOnDown(component2, m_AcceptButton);
			}
		}
		if (m_ToggleText != null)
		{
			m_ToggleText.SetLocalisedTextCatchAll(m_ControlSchemes[m_ControlSchemeIndex].m_Name);
		}
		bool active = m_ControlSchemes[m_ControlSchemeIndex].m_ControlType == GamepadUser.ControlTypeEnum.Keyboard;
		if (m_DefaultsButton != null)
		{
			m_DefaultsButton.gameObject.SetActive(active);
		}
		if (m_AcceptButton != null)
		{
			m_AcceptButton.gameObject.SetActive(active);
		}
	}

	private void SetClickListener(T17Button button, UnityAction listener)
	{
		if (button != null && listener != null)
		{
			button.onClick.AddListener(listener);
		}
	}

	private void SetNavigationOnDown(Selectable obj, Selectable target)
	{
		if (obj != null && target != null)
		{
			Navigation navigation = obj.navigation;
			navigation.selectOnDown = target;
			obj.navigation = navigation;
		}
	}

	private void SetNavigationOnUp(Selectable obj, Selectable target)
	{
		if (obj != null && target != null)
		{
			Navigation navigation = obj.navigation;
			navigation.selectOnUp = target;
			obj.navigation = navigation;
		}
	}
}
