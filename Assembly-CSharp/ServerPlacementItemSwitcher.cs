using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerPlacementItemSwitcher : ServerSynchroniserBase, ITriggerReceiver
{
	private PlacementItemSwitcher m_placementItemSwitcher;

	private ServerAttachStation m_attachStation;

	private int m_currentItemPrefabIndex;

	private PickupItemSwitcherMessage m_message = new PickupItemSwitcherMessage();

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_placementItemSwitcher = (PlacementItemSwitcher)synchronisedObject;
		m_attachStation = GetComponent<ServerAttachStation>();
	}

	public override EntityType GetEntityType()
	{
		return EntityType.PickupItemSwitcher;
	}

	public void OnTrigger(string _trigger)
	{
		if (m_placementItemSwitcher.enabled && _trigger == m_placementItemSwitcher.m_switchTrigger)
		{
			m_currentItemPrefabIndex++;
			m_currentItemPrefabIndex %= m_placementItemSwitcher.m_ingredients.Length;
			m_message.m_itemIndex = m_currentItemPrefabIndex;
			SendServerEvent(m_message);
		}
	}
}
