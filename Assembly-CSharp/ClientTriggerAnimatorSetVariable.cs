using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientTriggerAnimatorSetVariable : ClientSynchroniserBase
{
	private TriggerAnimatorSetVariable m_triggerAnimatorVariable;

	public override EntityType GetEntityType()
	{
		return EntityType.AnimatorVariable;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_triggerAnimatorVariable = (TriggerAnimatorSetVariable)synchronisedObject;
	}

	public override void ApplyServerEvent(Serialisable serialisable)
	{
		TriggerAnimatorVariableMessage triggerAnimatorVariableMessage = (TriggerAnimatorVariableMessage)serialisable;
		if (triggerAnimatorVariableMessage != null)
		{
			AnimatorUtils.SetValue(m_triggerAnimatorVariable.m_targetAnimator, m_triggerAnimatorVariable.m_variableNameHash, m_triggerAnimatorVariable.m_variableType, GetValue(triggerAnimatorVariableMessage));
		}
	}

	public object GetValue(TriggerAnimatorVariableMessage _data)
	{
		switch (m_triggerAnimatorVariable.m_variableType)
		{
		case AnimatorVariableType.Bool:
			if (m_triggerAnimatorVariable.m_randomValue && _data.Type != TriggerAnimatorVariableMessage.RandomValueType.None)
			{
				return _data.m_randomValue;
			}
			return m_triggerAnimatorVariable.m_boolValue;
		case AnimatorVariableType.Int:
			if (m_triggerAnimatorVariable.m_randomValue && _data.Type != TriggerAnimatorVariableMessage.RandomValueType.None)
			{
				return _data.m_randomValue;
			}
			return m_triggerAnimatorVariable.m_intValue;
		case AnimatorVariableType.Float:
			if (m_triggerAnimatorVariable.m_randomValue && _data.Type != TriggerAnimatorVariableMessage.RandomValueType.None)
			{
				return _data.m_randomValue;
			}
			return m_triggerAnimatorVariable.m_floatValue;
		default:
			return null;
		}
	}
}
