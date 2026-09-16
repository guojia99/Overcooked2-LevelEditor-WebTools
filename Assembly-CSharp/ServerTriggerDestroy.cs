using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerTriggerDestroy : ServerSynchroniserBase, ITriggerReceiver
{
	private TriggerDestroy m_trigger;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_trigger = (TriggerDestroy)synchronisedObject;
	}

	public void OnTrigger(string _name)
	{
		if (_name == m_trigger.m_trigger)
		{
			NetworkUtils.DestroyObject(base.gameObject);
		}
	}
}
