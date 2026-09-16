using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerTriggerAnimatorSetVariable : ServerSynchroniserBase, ITriggerReceiver
{
	private TriggerAnimatorSetVariable m_triggerAnimatorVariable;

	private TriggerAnimatorVariableMessage m_data = new TriggerAnimatorVariableMessage();

	public override EntityType GetEntityType()
	{
		return EntityType.AnimatorVariable;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_triggerAnimatorVariable = (TriggerAnimatorSetVariable)synchronisedObject;
	}

	private void SendTriggerMessage()
	{
		if (m_triggerAnimatorVariable.m_randomValue)
		{
			switch (m_triggerAnimatorVariable.m_variableType)
			{
			case AnimatorVariableType.Bool:
				m_data.InitRandomBool();
				break;
			case AnimatorVariableType.Float:
				m_data.InitRandomFloat(m_triggerAnimatorVariable.m_minFloatValue, m_triggerAnimatorVariable.m_maxFloatValue);
				break;
			case AnimatorVariableType.Int:
				m_data.InitRandomInt(m_triggerAnimatorVariable.m_minIntValue, m_triggerAnimatorVariable.m_maxIntValue);
				break;
			}
		}
		else
		{
			m_data.Initialise();
		}
		SendServerEvent(m_data);
	}

	public override void UpdateSynchronising()
	{
		if (!(m_triggerAnimatorVariable == null) && m_triggerAnimatorVariable.enabled && m_triggerAnimatorVariable.m_onAwake)
		{
			m_triggerAnimatorVariable.m_onAwake = false;
			SendTriggerMessage();
		}
	}

	public void OnTrigger(string _trigger)
	{
		if (m_triggerAnimatorVariable.enabled && m_triggerAnimatorVariable.m_triggerToReceive == _trigger)
		{
			SendTriggerMessage();
		}
	}
}
