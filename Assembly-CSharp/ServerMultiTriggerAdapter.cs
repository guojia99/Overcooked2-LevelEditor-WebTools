using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerMultiTriggerAdapter : ServerSynchroniserBase, ITriggerReceiver
{
	private MultiTriggerAdapter m_adapter;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_adapter = (MultiTriggerAdapter)synchronisedObject;
	}

	public void OnTrigger(string _trigger)
	{
		for (int i = 0; i < m_adapter.m_adapters.Count; i++)
		{
			if (_trigger == m_adapter.m_adapters[i].m_inputTrigger)
			{
				base.gameObject.SendTrigger(m_adapter.m_adapters[i].m_outputTrigger);
			}
		}
	}
}
