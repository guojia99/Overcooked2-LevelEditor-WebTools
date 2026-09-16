using System;
using System.Collections.Generic;
using UnityEngine;

public class TrailPFXCosmeticDecisions : MonoBehaviour
{
	private struct InitParticleData
	{
		public Vector3 m_scale;

		public float m_particleSize;
	}

	[SerializeField]
	private ParticleSystem[] m_movingPFX = new ParticleSystem[0];

	[SerializeField]
	private ParticleSystem[] m_staticPFX = new ParticleSystem[0];

	private Dictionary<ParticleSystem, InitParticleData> m_initParticleDataLookup = new Dictionary<ParticleSystem, InitParticleData>();

	private Rigidbody m_rigidBody;

	private Vector3 m_prevPosition;

	private void RecordInitParticle(ParticleSystem _system)
	{
		InitParticleData value = new InitParticleData
		{
			m_scale = _system.transform.localScale,
			m_particleSize = _system.startSize
		};
		m_initParticleDataLookup.Add(_system, value);
	}

	private void Start()
	{
		for (int i = 0; i < m_movingPFX.Length; i++)
		{
			RecordInitParticle(m_movingPFX[i]);
			m_movingPFX[i].Play();
		}
		for (int j = 0; j < m_staticPFX.Length; j++)
		{
			RecordInitParticle(m_staticPFX[j]);
			m_staticPFX[j].Play();
		}
		if (base.transform.parent != null)
		{
			m_rigidBody = base.transform.parent.GetComponent<Rigidbody>();
			m_prevPosition = base.transform.parent.position;
		}
	}

	private void LateUpdate()
	{
		if (m_rigidBody != null)
		{
			Vector3 position = base.transform.parent.position;
			Vector3 vector = position - m_prevPosition;
			m_prevPosition = position;
			float deltaTime = TimeManager.GetDeltaTime(base.gameObject);
			float num = Mathf.Clamp01(vector.magnitude / (0.5f * Mathf.Max(deltaTime, 0.0001f)));
			float num2 = Mathf.Sin((float)Math.PI / 2f * num);
			float num3 = Mathf.Cos((float)Math.PI / 2f * num);
			for (int i = 0; i < m_movingPFX.Length; i++)
			{
				InitParticleData initParticleData = m_initParticleDataLookup[m_movingPFX[i]];
				m_movingPFX[i].startSize = num2 * initParticleData.m_particleSize;
			}
			for (int j = 0; j < m_staticPFX.Length; j++)
			{
				InitParticleData initParticleData2 = m_initParticleDataLookup[m_staticPFX[j]];
				m_staticPFX[j].startSize = num3 * initParticleData2.m_particleSize;
			}
		}
	}
}
