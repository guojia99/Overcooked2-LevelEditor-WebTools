using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class AnimatedParticleEmission : MonoBehaviour
{
	[SerializeField]
	[AssignComponent(Editorbility.NonEditable)]
	private ParticleSystem m_particleSystem;

	[SerializeField]
	private bool m_emit;

	private void LateUpdate()
	{
		m_particleSystem.enableEmission = m_emit;
	}
}
