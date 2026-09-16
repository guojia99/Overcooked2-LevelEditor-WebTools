using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerTriggerDisableScript : ServerSynchroniserBase, ITriggerReceiver
{
	private TriggerDisableScript m_triggerDisable;

	private TriggerDisableMessage m_data = new TriggerDisableMessage();

	public override EntityType GetEntityType()
	{
		return EntityType.TriggerDisable;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_triggerDisable = (TriggerDisableScript)synchronisedObject;
	}

	private void SetEnabled(bool _enabled)
	{
		m_data.Initialise(_enabled);
		SendServerEvent(m_data);
	}

	public void OnTrigger(string _trigger)
	{
		if (m_triggerDisable.m_enableTrigger == _trigger)
		{
			SetEnabled(true);
		}
		if (m_triggerDisable.m_disableTrigger == _trigger)
		{
			SetEnabled(false);
		}
	}
}
