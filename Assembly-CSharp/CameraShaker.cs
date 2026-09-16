using System;
using UnityEngine;

public class CameraShaker : MonoBehaviour
{
	[Range(0f, 1f)]
	public float Power = 1f;

	[Header("X")]
	public float DistanceX = 0.03f;

	public float FrequencyX = 1f;

	public float[] PhasesX = new float[0];

	[Header("Y")]
	public float DistanceY = 0.03f;

	public float FrequencyY = 1f;

	public float[] PhasesY = new float[1] { 1f };

	[Header("Z")]
	public float DistanceZ = 0.03f;

	public float FrequencyZ = 1f;

	public float[] PhasesZ = new float[0];

	private float m_timer;

	private float GetProp(float[] _phases, float _frequency, float _timer)
	{
		float num = 0f;
		if (_phases.Length > 0)
		{
			for (int i = 0; i < _phases.Length; i++)
			{
				num += Mathf.Sin(_frequency * _phases[i] * _timer);
			}
			num /= (float)_phases.Length;
		}
		return num;
	}

	private void Update()
	{
		float prop = GetProp(PhasesX, FrequencyX, m_timer);
		float prop2 = GetProp(PhasesY, FrequencyY, m_timer);
		float prop3 = GetProp(PhasesZ, FrequencyZ, m_timer);
		Vector3 localPosition = Power * new Vector3(DistanceX * prop, DistanceY * prop2, DistanceZ * prop3);
		base.transform.localPosition = localPosition;
		m_timer += TimeManager.GetDeltaTime(base.gameObject);
		m_timer %= (float)Math.PI * 1000000f;
	}
}
