using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientBackpackDispenser : ClientSynchroniserBase, IClientHandlePickup, IBaseHandlePickup
{
	private Backpack m_backpack;

	private ClientPickupItemSpawner m_itemSpawner;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_backpack = synchronisedObject as Backpack;
	}

	public bool CanHandlePickup(ICarrier _carrier)
	{
		return m_backpack.CanHandleDispenserPickup(_carrier);
	}

	public int GetPickupPriority()
	{
		return 0;
	}
}
