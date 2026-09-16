using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientTriggerToggleOnAnimator : ClientSynchroniserBase
{
	private TriggerToggleOnAnimator m_triggerOnAnimator;

	private bool m_currentValue;

	public bool CurrentState
	{
		get
		{
			if (m_triggerOnAnimator.m_targetAnimator != null)
			{
				return m_triggerOnAnimator.m_targetAnimator.GetBool(m_triggerOnAnimator.m_targetParameterHash);
			}
			return m_currentValue;
		}
	}

	public override EntityType GetEntityType()
	{
		return EntityType.TriggerToggleOnAnimator;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_triggerOnAnimator = (TriggerToggleOnAnimator)synchronisedObject;
		if (m_triggerOnAnimator.m_targetAnimator != null)
		{
			m_currentValue = m_triggerOnAnimator.m_initialValue;
			m_triggerOnAnimator.m_targetAnimator.SetBool(m_triggerOnAnimator.m_targetParameterHash, m_currentValue);
		}
	}

	public override void ApplyServerEvent(Serialisable serialisable)
	{
		TriggerToggleOnAnimatorMessage triggerToggleOnAnimatorMessage = (TriggerToggleOnAnimatorMessage)serialisable;
		SetAnimatorState(triggerToggleOnAnimatorMessage.m_value);
	}

	private void SetAnimatorState(bool _state)
	{
		if (m_triggerOnAnimator.enabled && m_triggerOnAnimator.m_targetAnimator != null)
		{
			m_currentValue = _state;
			m_triggerOnAnimator.m_targetAnimator.SetBool(m_triggerOnAnimator.m_targetParameterHash, _state);
		}
	}
}
