using System;
using UnityEngine;

[RequireComponent(typeof(Light))]
public abstract class LightModifier : MonoBehaviour
{
	[SerializeField]
	private int m_applicationOrder;

	private LightModifier[] m_allmodifiers;

	private Light m_light;

	protected Light BaseLight
	{
		get
		{
			return m_light;
		}
	}

	protected virtual void Awake()
	{
		m_allmodifiers = base.gameObject.GetComponents<LightModifier>();
		Array.Sort(m_allmodifiers, (LightModifier x, LightModifier y) => x.m_applicationOrder.CompareTo(y.m_applicationOrder));
		m_light = base.gameObject.RequireComponent<Light>();
	}

	protected void Start()
	{
		Update();
	}

	protected void Update()
	{
		if (m_allmodifiers[0] == this)
		{
			for (int i = 0; i < m_allmodifiers.Length; i++)
			{
				m_allmodifiers[i].ModifyLight(m_light);
			}
		}
	}

	protected abstract void ModifyLight(Light _light);
}
