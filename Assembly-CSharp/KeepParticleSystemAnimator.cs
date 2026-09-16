using System.Collections;
using UnityEngine;

public class KeepParticleSystemAnimator : MonoBehaviour
{
	private const float c_updateInterval = 0.25f;

	private const float c_updateIntervalRandomness = 0.05f;

	private KeepParticleAnimatorData[] m_datas = new KeepParticleAnimatorData[0];

	private ParticleSystem[] m_particleSystems = new ParticleSystem[0];

	private float m_updateInterval = 0.25f;

	private IEnumerator m_updateRoutine;
}
