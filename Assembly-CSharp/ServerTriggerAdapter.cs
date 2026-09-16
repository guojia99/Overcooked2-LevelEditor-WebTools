using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerTriggerAdapter : ServerSynchroniserBase, ITriggerReceiver
{
	private TriggerAdapter m_adapter;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_adapter = (TriggerAdapter)synchronisedObject;
	}

	public void OnTrigger(string _trigger)
	{
		if (_trigger == m_adapter.m_inputTrigger)
		{
			base.gameObject.SendTrigger(m_adapter.m_outputTrigger);
		}
	}
}
