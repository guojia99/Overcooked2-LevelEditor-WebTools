using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientFireHazardSpawner : ClientSynchroniserBase
{
	private FireHazardSpawner m_spawner;

	public override EntityType GetEntityType()
	{
		return EntityType.FireHazardSpawner;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_spawner = (FireHazardSpawner)synchronisedObject;
		NetworkUtils.RegisterSpawnablePrefab(base.gameObject, m_spawner.m_hazardPrefab, OnHazardSpawned);
	}

	private void OnHazardSpawned(GameObject _object)
	{
	}

	public override void ApplyServerEvent(Serialisable serialisable)
	{
		if (!ConnectionStatus.IsInSession() || ConnectionStatus.IsHost())
		{
			return;
		}
		FireHazardSpawnerMessage fireHazardSpawnerMessage = (FireHazardSpawnerMessage)serialisable;
		EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(fireHazardSpawnerMessage.m_parentEntry.m_Header.m_uEntityID);
		if (entry == null)
		{
			return;
		}
		GameObject gameObject = entry.m_GameObject;
		if (gameObject != null)
		{
			IParentable parentable = gameObject.RequestInterface<IParentable>();
			if (parentable != null)
			{
				Transform attachPoint = parentable.GetAttachPoint(base.gameObject);
				GameObject gameObject2 = Object.Instantiate(m_spawner.m_hazardPrefab, attachPoint.position, attachPoint.rotation, attachPoint);
				EntitySerialisationRegistry.RegisterObject(gameObject2, fireHazardSpawnerMessage.m_spawnedEntry.m_Header.m_uEntityID);
				ComponentCacheRegistry.UpdateObject(gameObject2);
			}
		}
	}
}
