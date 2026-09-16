using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(WindVolume))]
public class WindCosmeticDecisions : MonoBehaviour
{
	[Serializable]
	private class MaterialLerp
	{
		public float MinValue;

		public float MaxValue;

		public float LerpTime;
	}

	[SerializeField]
	private float m_windSpeedMin;

	[SerializeField]
	private string m_materialProperty = string.Empty;

	[SerializeField]
	private MaterialLerp m_lerp = new MaterialLerp();

	private ParticleSystem[] m_particleSystems;

	private MeshRenderer[] m_meshRenderers;

	private WindVolume m_volume;

	private MaterialPropertyBlock m_materialPropertyBlock;

	private int m_materialPropertyID = -1;

	private float m_materialValue;

	private IEnumerator m_lerpRoutine;

	private bool m_showing;

	private void Awake()
	{
		m_volume = base.gameObject.RequireComponent<WindVolume>();
		m_particleSystems = base.gameObject.RequestComponentsRecursive<ParticleSystem>();
		m_meshRenderers = base.gameObject.RequestComponentsRecursive<MeshRenderer>();
		m_materialPropertyID = Shader.PropertyToID(m_materialProperty);
	}

	private void OnEnable()
	{
		m_showing = false;
		for (int i = 0; i < m_particleSystems.Length; i++)
		{
			ParticleSystem particleSystem = m_particleSystems[i];
			particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
		}
		SetParticlesEnabled(false);
		for (int j = 0; j < m_particleSystems.Length; j++)
		{
			ParticleSystem particleSystem2 = m_particleSystems[j];
			particleSystem2.Play(true);
		}
		m_materialValue = m_lerp.MinValue;
		ApplyMaterialFade(m_materialValue);
	}

	private void Update()
	{
		if (m_volume.enabled && m_volume.GetVelocity().magnitude > m_windSpeedMin)
		{
			if (!m_showing)
			{
				m_showing = true;
				SetParticlesEnabled(true);
				m_lerpRoutine = LerpTowardsValue(m_materialValue, m_lerp.MaxValue, m_lerp);
			}
		}
		else if (m_showing)
		{
			m_showing = false;
			SetParticlesEnabled(false);
			m_lerpRoutine = LerpTowardsValue(m_materialValue, m_lerp.MinValue, m_lerp);
		}
		if (m_lerpRoutine != null)
		{
			if (m_lerpRoutine.MoveNext())
			{
				m_materialValue = (float)m_lerpRoutine.Current;
				ApplyMaterialFade(m_materialValue);
			}
			else
			{
				m_lerpRoutine = null;
			}
		}
	}

	private IEnumerator LerpTowardsValue(float _value, float _targetValue, MaterialLerp _lerp)
	{
		float startValue = _value;
		float remaining = Mathf.Abs(_targetValue - _value) / Mathf.Abs(_lerp.MaxValue - _lerp.MinValue);
		float speed = 1f / _lerp.LerpTime;
		while (remaining > 0f)
		{
			remaining -= speed * TimeManager.GetDeltaTime(base.gameObject.layer);
			_value = Mathf.Lerp(startValue, _targetValue, 1f - remaining);
			yield return _value;
		}
	}

	private void SetParticlesEnabled(bool _enabled)
	{
		for (int i = 0; i < m_particleSystems.Length; i++)
		{
			ParticleSystem particleSystem = m_particleSystems[i];
			ParticleSystem.EmissionModule emission = particleSystem.emission;
			emission.enabled = _enabled;
		}
	}

	private void ApplyMaterialFade(float _value)
	{
		if (m_materialPropertyBlock == null)
		{
			m_materialPropertyBlock = new MaterialPropertyBlock();
		}
		m_materialPropertyBlock.SetFloat(m_materialPropertyID, _value);
		for (int i = 0; i < m_meshRenderers.Length; i++)
		{
			m_meshRenderers[i].SetPropertyBlock(m_materialPropertyBlock);
		}
	}
}
