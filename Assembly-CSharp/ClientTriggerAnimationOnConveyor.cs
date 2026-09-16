using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientTriggerAnimationOnConveyor : ClientSynchroniserBase
{
	private TriggerAnimationOnConveyor m_triggerAnimationOnConveyor;

	private Animator m_animator;

	private TriggerAnimationOnConveyor.State m_state;

	public override EntityType GetEntityType()
	{
		return EntityType.ConveyorAnimator;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_triggerAnimationOnConveyor = (TriggerAnimationOnConveyor)synchronisedObject;
	}

	public override void ApplyServerEvent(Serialisable serialisable)
	{
		ConveyorAnimationMessage conveyorAnimationMessage = (ConveyorAnimationMessage)serialisable;
		if (conveyorAnimationMessage.m_state != m_state)
		{
			m_state = conveyorAnimationMessage.m_state;
			if (m_state == TriggerAnimationOnConveyor.State.Animating)
			{
				m_animator.SetTrigger(m_triggerAnimationOnConveyor.m_animationStartTriggerHash);
			}
		}
	}

	public override void UpdateSynchronising()
	{
		if (m_animator == null)
		{
			m_animator = base.gameObject.RequestComponent<Animator>();
		}
	}
}
