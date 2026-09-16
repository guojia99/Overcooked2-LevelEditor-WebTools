using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientTriggerDisableScript : ClientSynchroniserBase
{
	private TriggerDisableScript m_triggerDisable;

	public override EntityType GetEntityType()
	{
		return EntityType.TriggerDisable;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_triggerDisable = (TriggerDisableScript)synchronisedObject;
		if (m_triggerDisable.m_script != null)
		{
			m_triggerDisable.m_script.enabled = m_triggerDisable.m_startEnabled;
		}
	}

	public override void ApplyServerEvent(Serialisable serialisable)
	{
		TriggerDisableMessage triggerDisableMessage = (TriggerDisableMessage)serialisable;
		if (m_triggerDisable.m_script != null)
		{
			m_triggerDisable.m_script.enabled = triggerDisableMessage.m_enabled;
		}
	}
}
