using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientPickupItemSwitcher : ClientSynchroniserBase
{
	private PickupItemSwitcher m_pickupItemSwitcher;

	private PickupItemSpawner m_pickupItemSpawner;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_pickupItemSwitcher = (PickupItemSwitcher)synchronisedObject;
		for (int i = 0; i < m_pickupItemSwitcher.m_itemPrefabs.Length; i++)
		{
			NetworkUtils.RegisterSpawnablePrefab(base.gameObject, m_pickupItemSwitcher.m_itemPrefabs[i], OnItemSpawned);
		}
		m_pickupItemSpawner = m_pickupItemSwitcher.GetComponent<PickupItemSpawner>();
		if (m_pickupItemSwitcher.m_itemPrefabs.Length > 0)
		{
			m_pickupItemSpawner.m_itemPrefab = m_pickupItemSwitcher.m_itemPrefabs[0];
		}
	}

	public override void ApplyServerEvent(Serialisable serialisable)
	{
		PickupItemSwitcherMessage pickupItemSwitcherMessage = (PickupItemSwitcherMessage)serialisable;
		m_pickupItemSpawner.m_itemPrefab = m_pickupItemSwitcher.m_itemPrefabs[pickupItemSwitcherMessage.m_itemIndex];
		base.gameObject.SendMessage("OnItemSwitched", SendMessageOptions.DontRequireReceiver);
	}

	public override EntityType GetEntityType()
	{
		return EntityType.PickupItemSwitcher;
	}

	public void OnItemSpawned(GameObject _spawned)
	{
		base.gameObject.SendMessage("OnPickupItem", SendMessageOptions.DontRequireReceiver);
	}
}
