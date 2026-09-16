using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientMultiTriggerDisableScript : ClientSynchroniserBase
{
	private MultiTriggerDisableScript m_triggerDisable;

	public override EntityType GetEntityType()
	{
		return EntityType.MultiTriggerDisable;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_triggerDisable = (MultiTriggerDisableScript)synchronisedObject;
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
