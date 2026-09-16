using UnityEngine;

[RequireComponent(typeof(Light))]
public class MaterialColourLight : LightModifier
{
	[SerializeField]
	private Renderer m_renderer;

	private Material m_material;

	protected override void Awake()
	{
		base.Awake();
		if (m_renderer != null)
		{
			m_material = m_renderer.material;
		}
	}

	protected override void ModifyLight(Light _light)
	{
		_light.color = m_material.color;
	}
}
