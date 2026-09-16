using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerBackpackDispenser : ServerSynchroniserBase, IHandlePickup, IBaseHandlePickup
{
	private Backpack m_backpack;

	private ServerPickupItemSpawner m_itemSpawner;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_backpack = synchronisedObject as Backpack;
		m_itemSpawner = base.gameObject.RequireComponent<ServerPickupItemSpawner>();
	}

	public bool CanHandlePickup(ICarrier _carrier)
	{
		return m_backpack.CanHandleDispenserPickup(_carrier) && m_itemSpawner.CanHandlePickup(_carrier);
	}

	public int GetPickupPriority()
	{
		return 0;
	}

	public void HandlePickup(ICarrier _carrier, Vector2 _directionXZ)
	{
		ServerMessenger.Achievement(_carrier.AccessGameObject(), 502);
		m_itemSpawner.HandlePickup(_carrier, _directionXZ);
	}
}
