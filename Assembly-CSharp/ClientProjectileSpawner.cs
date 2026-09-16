using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientProjectileSpawner : ClientSynchroniserBase
{
	private ProjectileSpawner m_spawner;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_spawner = (ProjectileSpawner)synchronisedObject;
		NetworkUtils.RegisterSpawnablePrefab(base.gameObject, m_spawner.m_projectilePrefab, OnProjectileSpawned);
	}

	private void OnProjectileSpawned(GameObject _object)
	{
		ParticleSystem particleSystem = _object.RequestComponentRecursive<ParticleSystem>();
		if (particleSystem != null)
		{
			particleSystem.RestartPFX();
		}
	}

	private void ProjectileReachedTarget(Projectile _projectile)
	{
	}

	private void ProjectileCollided(Projectile _projectile, Collision _collision)
	{
	}
}
