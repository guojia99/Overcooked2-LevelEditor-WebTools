using System;
using UnityEngine;

[Serializable]
public class MaterialScroll
{
	[SerializeField]
	private Material m_material;

	[SerializeField]
	private float m_value;

	public void SetMaterialScrollToZero()
	{
		m_material.SetFloat("_ScrollSpeed", 0f);
	}

	public void SetMaterialScrollToValue()
	{
		m_material.SetFloat("_ScrollSpeed", m_value);
	}
}
