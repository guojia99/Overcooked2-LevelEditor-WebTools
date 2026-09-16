using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public abstract class ServerTimedQueue : ServerSynchroniserBase, ITriggerReceiver
{
	private TimedQueue m_timedQueue;

	private TimedQueueMessage m_data = new TimedQueueMessage();

	private IFlowController m_iFlowController;

	private bool m_intitialised;

	private float m_delayTimer = -1f;

	private int m_nextIndex = -1;

	public override EntityType GetEntityType()
	{
		return EntityType.TimedQueue;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_timedQueue = (TimedQueue)synchronisedObject;
		if (m_timedQueue.m_startOnAwake && base.gameObject.activeInHierarchy)
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

	private void SendEventForTime(int _index, float _time)
	{
		m_data.m_msgType = TimedQueueMessage.MsgType.QueueEvent;
		m_data.m_index = _index;
		m_data.m_time = _time;
		SendServerEvent(m_data);
	}

	private void SendCancelEvents()
	{
		m_data.m_msgType = TimedQueueMessage.MsgType.Cancel;
		SendServerEvent(m_data);
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

	protected virtual void Start()
	{
	}

	public override void UpdateSynchronising()
	{
		if (m_timedQueue == null)
		{
			return;
		}
		if (m_intitialised && m_timedQueue.m_startOnAwake)
		{
			m_nextIndex = 0;
			m_delayTimer = m_timedQueue.GetDelay(m_nextIndex);
			m_timedQueue.m_startOnAwake = false;
			SendEventForTime(m_nextIndex, ClientTime.Time() + m_delayTimer);
		}
		if (m_delayTimer >= 0f)
		{
			m_delayTimer -= TimeManager.GetDeltaTime(base.gameObject);
			if (m_delayTimer < 0f)
			{
				DoEvent(m_nextIndex);
			}
		}
	}

	protected virtual void DoEvent(int _index)
	{
	}

	protected void AdvanceQueue()
	{
		m_nextIndex++;
		if ((float)m_nextIndex < m_timedQueue.GetQueueLength())
		{
			m_delayTimer = m_timedQueue.GetDelay(m_nextIndex);
			SendEventForTime(m_nextIndex, ClientTime.Time() + m_delayTimer);
			return;
		}
		if (m_timedQueue.m_endTrigger != string.Empty)
		{
			GameObject endTriggerTarget = base.gameObject;
			if (m_timedQueue.m_endTriggerTarget != null)
			{
				endTriggerTarget = m_timedQueue.m_endTriggerTarget;
			}
			endTriggerTarget.SendTrigger(m_timedQueue.m_endTrigger);
		}
		if (m_timedQueue.m_loopWhenFinished)
		{
			m_nextIndex = 0;
			m_delayTimer = m_timedQueue.m_loopDelay;
			SendEventForTime(m_nextIndex, ClientTime.Time() + m_delayTimer);
		}
		else
		{
			m_nextIndex = -1;
			m_delayTimer = -1f;
		}
	}

	protected void ResetQueue()
	{
		m_nextIndex = -1;
		m_delayTimer = -1f;
		SendCancelEvents();
	}

	protected bool IsActive()
	{
		return m_nextIndex >= 0;
	}

	public void OnTrigger(string _trigger)
	{
		if (m_timedQueue.m_startTrigger == _trigger)
		{
			m_nextIndex = 0;
			m_delayTimer = m_timedQueue.GetDelay(m_nextIndex);
			SendEventForTime(m_nextIndex, ClientTime.Time() + m_delayTimer);
		}
		if (m_timedQueue.m_cancelTrigger == _trigger)
		{
			m_nextIndex = -1;
			m_delayTimer = -1f;
			SendCancelEvents();
		}
	}
}
