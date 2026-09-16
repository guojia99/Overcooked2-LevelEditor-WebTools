using System.Collections.Generic;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientPickupItemSpawner : ClientSynchroniserBase, IClientHandlePickup, IBaseHandlePickup
{
	private PickupItemSpawner m_pickupItemSpawner;

	private List<Generic<bool, ICarrier>> m_canHandlePickupCallbacks = new List<Generic<bool, ICarrier>>();

	private ClientFlammable m_flammable;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_pickupItemSpawner = (PickupItemSpawner)synchronisedObject;
		m_flammable = m_pickupItemSpawner.gameObject.RequestComponent<ClientFlammable>();
		NetworkUtils.RegisterSpawnablePrefab(base.gameObject, m_pickupItemSpawner.m_itemPrefab, OnItemSpawned);
	}

	public GameObject GetItemPrefab()
	{
		return m_pickupItemSpawner.m_itemPrefab;
	}

	private void OnItemSpawned(GameObject _spawned)
	{
		base.gameObject.SendMessage("OnPickupItem", SendMessageOptions.DontRequireReceiver);
	}

	public bool CanHandlePickup(ICarrier _carrier)
	{
		return (m_flammable == null || !m_flammable.OnFire()) && !m_canHandlePickupCallbacks.CallForResult(false, _carrier);
	}

	public int GetPickupPriority()
	{
		return m_pickupItemSpawner.m_pickupPriority;
	}
}
