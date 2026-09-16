using UnityEngine;

[RequireComponent(typeof(Light))]
public class FlickeringLight : LightModifier
{
	[SerializeField]
	private float m_intensityVariation = 2f;

	[SerializeField]
	private float m_rangeVariation = 1f;

	private float m_timer;

	private float m_initialIntensity;

	private float m_initialRange;

	protected override void Awake()
	{
		base.Awake();
		m_initialIntensity = base.BaseLight.intensity;
		m_initialRange = base.BaseLight.range;
	}

	protected override void ModifyLight(Light _light)
	{
		m_timer += TimeManager.GetDeltaTime(base.gameObject);
		float num = (Mathf.Sin(3.1f * m_timer) + Mathf.Sin(7f * m_timer) + Mathf.Sin(12f * m_timer)) / 3f;
		m_timer %= 1636.1415f;
		_light.intensity = m_initialIntensity + num * m_intensityVariation;
		_light.range = m_initialRange + num * m_rangeVariation;
	}
}
