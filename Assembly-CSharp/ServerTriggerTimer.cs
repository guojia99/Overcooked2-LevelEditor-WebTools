using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerTriggerTimer : ServerSynchroniserBase, ITriggerReceiver
{
	private TriggerTimer m_triggerTimer;

	private IFlowController m_iFlowController;

	private bool m_intitialised;

	private float m_timer;

	private bool m_triggeredAtStart;

	private bool m_startedTiming;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_triggerTimer = (TriggerTimer)synchronisedObject;
		if ((m_triggerTimer.m_triggerAtStart || m_triggerTimer.m_startTiming) && base.gameObject.activeInHierarchy)
		{
			m_iFlowController = GameUtils.GetFlowController();
			if (m_iFlowController != null)
			{
				m_iFlowController.RoundActivatedCallback += OnRoundBegun;
				m_intitialised = false;
			}
		}
		else
		{
			m_intitialised = true;
		}
	}

	public override void OnDestroy()
	{
		base.OnDestroy();
		if (m_iFlowController != null)
		{
			m_iFlowController.RoundActivatedCallback -= OnRoundBegun;
		}
	}

	private void OnRoundBegun()
	{
		m_intitialised = true;
	}

	protected override void OnEnable()
	{
		base.OnEnable();
		m_triggeredAtStart = false;
		m_startedTiming = false;
	}

	public void OnTrigger(string _trigger)
	{
		if (m_triggerTimer.m_startTrigger == _trigger)
		{
			m_timer = m_triggerTimer.m_time;
		}
	}

	public override void UpdateSynchronising()
	{
		if (m_triggerTimer == null || !m_triggerTimer.enabled)
		{
			return;
		}
		if (m_intitialised)
		{
			if (m_triggerTimer.m_startTiming && !m_startedTiming)
			{
				m_timer = m_triggerTimer.m_time;
				m_startedTiming = true;
			}
			if (m_triggerTimer.m_triggerAtStart && !m_triggeredAtStart)
			{
				base.gameObject.SendTrigger(m_triggerTimer.m_completeTrigger);
				m_triggeredAtStart = true;
			}
		}
		if (m_timer > 0f)
		{
			m_timer -= TimeManager.GetDeltaTime(base.gameObject);
			if (m_timer <= 0f)
			{
				base.gameObject.SendTrigger(m_triggerTimer.m_completeTrigger);
			}
		}
	}
}
