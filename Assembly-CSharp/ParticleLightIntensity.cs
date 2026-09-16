using UnityEngine;

[RequireComponent(typeof(Light))]
public class ParticleLightIntensity : LightModifier
{
	[SerializeField]
	private ParticleSystem m_particleSystem;

	protected override void ModifyLight(Light _light)
	{
		float num = (float)m_particleSystem.particleCount / (float)m_particleSystem.maxParticles;
		_light.intensity = num * _light.intensity;
	}
}
