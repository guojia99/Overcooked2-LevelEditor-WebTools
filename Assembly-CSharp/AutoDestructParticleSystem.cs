using UnityEngine;

public class AutoDestructParticleSystem : MonoBehaviour
{
	private ParticleSystem[] m_allParticles;

	private void Awake()
	{
		m_allParticles = base.gameObject.RequestComponentsRecursive<ParticleSystem>();
	}

	private void LateUpdate()
	{
		if (m_allParticles.FindIndex_Predicate((ParticleSystem x) => x != null && x.IsAlive()) == -1)
		{
			Object.Destroy(base.gameObject);
		}
	}
}
