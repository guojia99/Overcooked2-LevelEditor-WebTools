using System;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(T17Button))]
public class CarouselButton : MonoBehaviour
{
	private T17Button m_button;

	[HideInInspector]
	public CarouselRootMenu m_rootMenu;

	public Selectable Button
	{
		get
		{
			if (m_button == null)
			{
				m_button = base.gameObject.RequireComponent<T17Button>();
			}
			return m_button;
		}
	}

	protected virtual void Start()
	{
		Initialise();
	}

	protected virtual void OnDestroy()
	{
	}

	protected virtual void Initialise()
	{
		if (m_button == null)
		{
			m_button = base.gameObject.RequireComponent<T17Button>();
		}
		if (Application.isEditor)
		{
			ColorBlock colors = m_button.colors;
			colors.disabledColor = colors.normalColor;
			m_button.colors = colors;
		}
		T17Button button = m_button;
		button.OnButtonSelect = (T17Button.T17ButtonDelegate)Delegate.Combine(button.OnButtonSelect, new T17Button.T17ButtonDelegate(OnSelected));
	}

	protected void OnSelected(T17Button _button)
	{
		m_rootMenu.OnButtonSelected(this);
	}
}
