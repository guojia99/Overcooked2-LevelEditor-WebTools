using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerProjectileSpawner : ServerSynchroniserBase, ITriggerReceiver
{
	private ProjectileSpawner m_spawner;

	private GridManager m_gridManager;

	private int m_lastTarget = -1;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_spawner = (ProjectileSpawner)synchronisedObject;
		m_gridManager = GameUtils.GetGridManager(base.transform);
		NetworkUtils.RegisterSpawnablePrefab(base.gameObject, m_spawner.m_projectilePrefab);
	}

	private void Awake()
	{
	}

	private bool FindNextTarget(out Vector3 _position)
	{
		if (m_spawner.m_randomTargetOrder)
		{
			_position = ((!m_spawner.m_bUseTransformPositions) ? m_spawner.m_targetPositions.GetRandomElement() : m_spawner.m_transformTargetPositions.GetRandomElement().position);
			return true;
		}
		int num = m_lastTarget + 1;
		if (num >= m_spawner.m_targetPositions.Length)
		{
			num = 0;
		}
		if (num >= 0 && num < m_spawner.m_targetPositions.Length)
		{
			m_lastTarget = num;
			_position = m_spawner.m_targetPositions[num];
			return true;
		}
		_position = Vector3.zero;
		return false;
	}

	private bool FindNextTarget(out Transform _transform)
	{
		if (m_spawner.m_randomTargetOrder)
		{
			_transform = ((!m_spawner.m_bUseTransformPositions) ? m_spawner.m_transformTargetPositions.GetRandomElement() : m_spawner.m_transformTargetPositions.GetRandomElement());
			return true;
		}
		int num = m_lastTarget + 1;
		if (num >= m_spawner.m_transformTargetPositions.Length)
		{
			num = 0;
		}
		if (num >= 0 && num < m_spawner.m_transformTargetPositions.Length)
		{
			m_lastTarget = num;
			_transform = m_spawner.m_transformTargetPositions[num];
			return true;
		}
		_transform = null;
		return false;
	}

	private void SpawnProjectile(Vector3 _targetPos)
	{
		GameObject obj = NetworkUtils.ServerSpawnPrefab(base.gameObject, m_spawner.m_projectilePrefab, m_spawner.m_spawnPoint.position, Quaternion.identity);
		Collider collider = obj.RequestComponent<Collider>();
		if (collider != null)
		{
			Collider[] array = Physics.OverlapBox(collider.bounds.center, collider.bounds.extents);
			for (int i = 0; i < array.Length; i++)
			{
				Physics.IgnoreCollision(array[i], collider);
			}
		}
		ServerProjectile serverProjectile = obj.RequireComponent<ServerProjectile>();
		serverProjectile.RegisterReachedTargetCallback(ProjectileReachedTarget);
		serverProjectile.RegisterCollidedCallback(ProjectileCollided);
		serverProjectile.SetTargetAndTimeToTarget(_targetPos, m_spawner.m_airTime);
		if (m_spawner.m_fireMode == ProjectileSpawner.FireMode.Parabolic)
		{
			serverProjectile.SetGravity(Physics.gravity);
		}
	}

	private void SpawnProjectile(Transform _targetTransform)
	{
		GameObject obj = NetworkUtils.ServerSpawnPrefab(base.gameObject, m_spawner.m_projectilePrefab, m_spawner.m_spawnPoint.position, Quaternion.identity);
		Collider collider = obj.RequestComponent<Collider>();
		if (collider != null)
		{
			Collider[] array = Physics.OverlapBox(collider.bounds.center, collider.bounds.extents);
			for (int i = 0; i < array.Length; i++)
			{
				Physics.IgnoreCollision(array[i], collider);
			}
		}
		ServerProjectile serverProjectile = obj.RequireComponent<ServerProjectile>();
		serverProjectile.RegisterReachedTargetCallback(ProjectileReachedTarget);
		serverProjectile.RegisterCollidedCallback(ProjectileCollided);
		serverProjectile.SetTargetAndTimeToTarget(_targetTransform, m_spawner.m_airTime);
		ServerFireHazardSpawner component = serverProjectile.GetComponent<ServerFireHazardSpawner>();
		if (component != null)
		{
			component.SetTargetTransformToAttach(_targetTransform);
		}
		if (m_spawner.m_fireMode == ProjectileSpawner.FireMode.Parabolic)
		{
			serverProjectile.SetGravity(Physics.gravity);
		}
	}

	private void ProjectileReachedTarget(ServerProjectile _projectile)
	{
		if (m_spawner.m_reachedTargetTrigger != string.Empty)
		{
			base.gameObject.SendTrigger(m_spawner.m_reachedTargetTrigger);
			_projectile.gameObject.SendTrigger(m_spawner.m_reachedTargetTrigger);
		}
		_projectile.enabled = false;
	}

	private void ProjectileCollided(ServerProjectile _projectile, Collision _collision)
	{
		if (m_spawner.m_collidedTrigger != string.Empty)
		{
			base.gameObject.SendTrigger(m_spawner.m_collidedTrigger);
			_projectile.gameObject.SendTrigger(m_spawner.m_collidedTrigger);
		}
	}

	public void OnTrigger(string _trigger)
	{
		if (!(m_spawner.m_fireTrigger == _trigger))
		{
			return;
		}
		Vector3 _position;
		if (m_spawner.m_transformTargetPositions.Length > 0)
		{
			Transform _transform;
			if (FindNextTarget(out _transform))
			{
				SpawnProjectile(_transform);
				GameUtils.TriggerAudio(m_spawner.m_spawnAudioTag, base.gameObject.layer);
			}
		}
		else if (FindNextTarget(out _position))
		{
			if (m_spawner.m_alignTargetsToGrid)
			{
				GridIndex gridLocationFromPos = m_gridManager.GetGridLocationFromPos(_position);
				_position = m_gridManager.GetPosFromGridLocation(gridLocationFromPos);
			}
			SpawnProjectile(_position);
			GameUtils.TriggerAudio(m_spawner.m_spawnAudioTag, base.gameObject.layer);
		}
	}
}
