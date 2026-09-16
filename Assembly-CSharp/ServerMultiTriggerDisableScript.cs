using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerMultiTriggerDisableScript : ServerSynchroniserBase, ITriggerReceiver
{
	private MultiTriggerDisableScript m_triggerDisable;

	private bool[] m_triggers;

	private TriggerDisableMessage m_data = new TriggerDisableMessage();

	public override EntityType GetEntityType()
	{
		return EntityType.MultiTriggerDisable;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_triggerDisable = (MultiTriggerDisableScript)synchronisedObject;
		m_triggers = new bool[m_triggerDisable.m_triggers.Length];
		if (m_triggerDisable.m_startEnabled)
		{
			for (int i = 0; i < m_triggers.Length; i++)
			{
				m_triggers[i] = true;
			}
		}
	}

	private void SetEnabled(bool _enabled)
	{
		m_data.Initialise(_enabled);
		SendServerEvent(m_data);
	}

	public void OnTrigger(string _trigger)
	{
		bool flag = true;
		for (int i = 0; i < m_triggers.Length; i++)
		{
			TriggerPair triggerPair = m_triggerDisable.m_triggers[i];
			if (triggerPair.m_enableTrigger == _trigger)
			{
				m_triggers[i] = true;
			}
			else if (triggerPair.m_disableTrigger == _trigger)
			{
				m_triggers[i] = false;
			}
			flag &= m_triggers[i];
		}
		SetEnabled(flag);
	}
}
