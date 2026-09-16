using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerTriggerConveyorAdjacentUpdate : ServerSynchroniserBase, ITriggerReceiver
{
	private TriggerConveyorAdjacentUpdate m_adjacentUpdate;

	private ServerConveyorStation m_station;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_adjacentUpdate = (TriggerConveyorAdjacentUpdate)synchronisedObject;
		m_station = base.gameObject.RequireComponent<ServerConveyorStation>();
	}

	public void OnTrigger(string _trigger)
	{
		if (m_adjacentUpdate.m_updateTrigger == _trigger)
		{
			m_station.UpdateAdjacentReceiver();
		}
	}
}
