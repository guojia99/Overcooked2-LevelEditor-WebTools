using System.Collections.Generic;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public abstract class ClientTimedQueue : ClientSynchroniserBase
{
	private struct EventInfo
	{
		public int m_index;

		public float m_time;

		public EventInfo(int _index, float _time)
		{
			m_index = _index;
			m_time = _time;
		}
	}

	protected TimedQueue m_timedQueue;

	private List<EventInfo> m_pendingEvents = new List<EventInfo>();

	public override EntityType GetEntityType()
	{
		return EntityType.TimedQueue;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_timedQueue = (TimedQueue)synchronisedObject;
	}

	public override void ApplyServerEvent(Serialisable serialisable)
	{
		TimedQueueMessage timedQueueMessage = (TimedQueueMessage)serialisable;
		switch (timedQueueMessage.m_msgType)
		{
		case TimedQueueMessage.MsgType.QueueEvent:
			m_pendingEvents.Add(new EventInfo(timedQueueMessage.m_index, timedQueueMessage.m_time));
			break;
		case TimedQueueMessage.MsgType.Cancel:
			m_pendingEvents.Clear();
			break;
		}
	}

	public override void UpdateSynchronising()
	{
		float num = ClientTime.Time();
		if (m_pendingEvents.Count > 0)
		{
			for (int i = 0; i < m_pendingEvents.Count; i++)
			{
				if (m_pendingEvents[i].m_time <= num)
				{
					DoEvent(m_pendingEvents[i].m_index);
				}
			}
			for (int num2 = m_pendingEvents.Count - 1; num2 >= 0; num2--)
			{
				if (m_pendingEvents[num2].m_time <= num)
				{
					m_pendingEvents.RemoveAt(num2);
				}
			}
		}
		if (TimeManager.IsPaused(base.gameObject))
		{
			for (int j = 0; j < m_pendingEvents.Count; j++)
			{
				EventInfo value = m_pendingEvents[j];
				value.m_time += ClientTime.DeltaTime();
				m_pendingEvents[j] = value;
			}
		}
	}

	protected virtual void DoEvent(int _index)
	{
	}
}
