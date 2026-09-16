using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerTriggerCounter : ServerSynchroniserBase, ITriggerReceiver
{
	private TriggerCounter m_counter;

	private int m_iCounter;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_counter = (TriggerCounter)synchronisedObject;
	}

	public void OnTrigger(string _trigger)
	{
		if (_trigger != m_counter.m_inputTrigger)
		{
			return;
		}
		m_iCounter++;
		if (m_iCounter == m_counter.m_count)
		{
			base.gameObject.SendTrigger(m_counter.m_outputTrigger);
			if (m_counter.m_ResetOnCountReached)
			{
				m_iCounter = 0;
			}
		}
	}
}
