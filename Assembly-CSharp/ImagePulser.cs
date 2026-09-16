using System;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class ImagePulser : MonoBehaviour
{
	[SerializeField]
	private float m_pulseTimePeriod = 1f;

	private CallbackVoid m_pulseCallback = delegate
	{
	};

	private float m_speedMultiplier = 1f;

	private float m_pulseTimer;

	private Image m_image;

	public void SetPulseCallback(CallbackVoid _callback)
	{
		m_pulseCallback = _callback;
	}

	public void SetPulseSpeedMultiplier(float _multiplier)
	{
		m_speedMultiplier = _multiplier;
	}

	private void Awake()
	{
		m_image = base.gameObject.GetComponent<Image>();
		SetAlpha(0f);
	}

	private void Update()
	{
		float pulseTimer = m_pulseTimer;
		float deltaTime = TimeManager.GetDeltaTime(base.gameObject);
		m_pulseTimer += m_speedMultiplier * 2f * (float)Math.PI * deltaTime / m_pulseTimePeriod;
		if (pulseTimer < (float)Math.PI && m_pulseTimer >= (float)Math.PI)
		{
			m_pulseCallback();
		}
		m_pulseTimer %= (float)Math.PI * 2f;
		SetAlpha(0.5f * (1f - Mathf.Cos(m_pulseTimer)));
	}

	private void SetAlpha(float _a)
	{
		Color color = m_image.color;
		color.a = _a;
		m_image.color = color;
	}
}
