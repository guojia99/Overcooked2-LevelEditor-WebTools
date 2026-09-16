using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerPickupItemSwitcher : ServerSynchroniserBase, ITriggerReceiver
{
	private PickupItemSwitcher m_pickupItemSwitcher;

	private int m_currentItemPrefabIndex;

	private PickupItemSwitcherMessage m_message = new PickupItemSwitcherMessage();

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_pickupItemSwitcher = (PickupItemSwitcher)synchronisedObject;
		for (int i = 0; i < m_pickupItemSwitcher.m_itemPrefabs.Length; i++)
		{
			NetworkUtils.RegisterSpawnablePrefab(base.gameObject, m_pickupItemSwitcher.m_itemPrefabs[i]);
		}
	}

	public override EntityType GetEntityType()
	{
		return EntityType.PickupItemSwitcher;
	}

	public void OnTrigger(string _trigger)
	{
		if (m_pickupItemSwitcher.enabled && _trigger == m_pickupItemSwitcher.m_switchTrigger)
		{
			m_currentItemPrefabIndex++;
			m_currentItemPrefabIndex %= m_pickupItemSwitcher.m_itemPrefabs.Length;
			m_message.m_itemIndex = m_currentItemPrefabIndex;
			SendServerEvent(m_message);
		}
	}
}
