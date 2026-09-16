using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerTriggerAnimationOnConveyor : ServerSynchroniserBase
{
	private TriggerAnimationOnConveyor m_triggerAnimationOnConveyor;

	private ConveyorAnimationMessage m_data = new ConveyorAnimationMessage();

	private ServerConveyorStation m_station;

	private IConveyenceReceiver m_receiver;

	private Animator m_animator;

	private TriggerAnimationOnConveyor.State m_state;

	public override EntityType GetEntityType()
	{
		return EntityType.ConveyorAnimator;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_receiver = base.gameObject.RequestInterface<IConveyenceReceiver>();
		m_station = base.gameObject.RequireComponent<ServerConveyorStation>();
		if (m_triggerAnimationOnConveyor.m_stopWhileAnimating)
		{
			m_station.RegisterAllowConveyCallback(AllowConvey);
			m_receiver.RegisterAllowConveyToCallback(AllowConvey);
		}
		m_triggerAnimationOnConveyor.RegisterOnTriggerCallback(OnTrigger);
	}

	private void SendStateChange()
	{
		m_data.Initialise(m_state);
		SendServerEvent(m_data);
	}

	private void Awake()
	{
		m_triggerAnimationOnConveyor = base.gameObject.RequireComponent<TriggerAnimationOnConveyor>();
		m_triggerAnimationOnConveyor.RegisterOnTriggerCallback(OnTrigger);
	}

	private bool AllowConvey()
	{
		return !m_triggerAnimationOnConveyor.m_stopWhileAnimating || m_state == TriggerAnimationOnConveyor.State.Idle;
	}

	private void OnTrigger(string _trigger)
	{
		if (m_triggerAnimationOnConveyor.m_startTrigger == _trigger)
		{
			m_state = TriggerAnimationOnConveyor.State.Pending;
			SendStateChange();
		}
		if (m_triggerAnimationOnConveyor.m_animationFinishedTrigger == _trigger)
		{
			m_state = TriggerAnimationOnConveyor.State.Idle;
			SendStateChange();
		}
	}

	public override void UpdateSynchronising()
	{
		if (m_animator == null)
		{
			m_animator = base.gameObject.RequestComponent<Animator>();
		}
		if (m_state == TriggerAnimationOnConveyor.State.Pending && !m_station.IsConveying() && (m_receiver == null || !m_receiver.IsReceiving()))
		{
			m_state = TriggerAnimationOnConveyor.State.Animating;
			SendStateChange();
		}
	}
}
