using System.Collections.Generic;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerPickupItemSpawner : ServerSynchroniserBase, IHandlePickup, IBaseHandlePickup
{
	private PickupItemSpawner m_pickupItemSpawner;

	private List<Generic<bool, ICarrier>> m_canHandlePickupCallbacks = new List<Generic<bool, ICarrier>>();

	private ServerFlammable m_flammable;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_pickupItemSpawner = (PickupItemSpawner)synchronisedObject;
		m_flammable = m_pickupItemSpawner.gameObject.RequestComponent<ServerFlammable>();
		NetworkUtils.RegisterSpawnablePrefab(base.gameObject, m_pickupItemSpawner.m_itemPrefab);
	}

	public GameObject GetItemPrefab()
	{
		return m_pickupItemSpawner.m_itemPrefab;
	}

	public int GetPickupPriority()
	{
		return m_pickupItemSpawner.m_pickupPriority;
	}

	public bool CanHandlePickup(ICarrier _carrier)
	{
		return (m_flammable == null || !m_flammable.OnFire()) && !m_canHandlePickupCallbacks.CallForResult(false, _carrier);
	}

	public void HandlePickup(ICarrier _carrier, Vector2 _directionXZ)
	{
		Vector3 position = base.gameObject.transform.position;
		IParentable parentable = _carrier.AccessGameObject().RequestInterface<IParentable>();
		if (parentable as MonoBehaviour != null)
		{
			position = parentable.GetAttachPoint(m_pickupItemSpawner.m_itemPrefab).position;
		}
		GameObject gameObject = NetworkUtils.ServerSpawnPrefab(base.gameObject, m_pickupItemSpawner.m_itemPrefab, position, Quaternion.identity);
		_carrier.CarryItem(gameObject);
	}
}
