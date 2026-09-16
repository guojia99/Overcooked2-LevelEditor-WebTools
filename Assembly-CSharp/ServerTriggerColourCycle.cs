using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerTriggerColourCycle : ServerSynchroniserBase, ITriggerReceiver
{
	private TriggerColourCycle m_triggerColourCycle;

	private int m_currentColourIndex;

	private TriggerColourCycleMessage m_message = new TriggerColourCycleMessage();

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_triggerColourCycle = (TriggerColourCycle)synchronisedObject;
		m_currentColourIndex = 0;
		m_message.m_colourIndex = 0;
	}

	public override EntityType GetEntityType()
	{
		return EntityType.TriggerColourCycle;
	}

	public void OnTrigger(string _trigger)
	{
		if (m_triggerColourCycle.m_trigger == _trigger)
		{
			m_currentColourIndex = (m_currentColourIndex + 1) % m_triggerColourCycle.m_materials.Length;
			m_message.m_colourIndex = m_currentColourIndex;
			SendServerEvent(m_message);
		}
	}
}
