using System;
using UnityEngine;

public class AnimationEventData : MonoBehaviour
{
	[Serializable]
	public class EventData
	{
		public string m_name;

		public float m_time;
	}

	[Serializable]
	public class ClipData
	{
		public string m_name;

		public float m_duration;

		public EventData[] m_events = new EventData[0];
	}

	[SerializeField]
	private ClipData[] m_clips = new ClipData[0];

	public ClipData[] clips
	{
		get
		{
			return m_clips;
		}
		set
		{
			m_clips = value;
		}
	}

	public void Copy(AnimationEventData _other)
	{
		m_clips = new ClipData[_other.m_clips.Length];
		for (int i = 0; i < _other.m_clips.Length; i++)
		{
			m_clips[i] = new ClipData();
			m_clips[i].m_name = _other.m_clips[i].m_name;
			m_clips[i].m_duration = _other.m_clips[i].m_duration;
			m_clips[i].m_events = new EventData[_other.m_clips[i].m_events.Length];
			for (int j = 0; j < _other.m_clips[i].m_events.Length; j++)
			{
				m_clips[i].m_events[j] = new EventData();
				m_clips[i].m_events[j].m_name = _other.m_clips[i].m_events[j].m_name;
				m_clips[i].m_events[j].m_time = _other.m_clips[i].m_events[j].m_time;
			}
		}
	}

	public bool GetTriggerData(string _clipName, string _triggerName, out float o_clipDuration, out float o_triggerTime)
	{
		ClipData clipData = Array.Find(m_clips, (ClipData x) => x.m_name == _clipName);
		EventData eventData = Array.Find(clipData.m_events, (EventData x) => x.m_name == _triggerName);
		o_clipDuration = clipData.m_duration;
		o_triggerTime = eventData.m_time;
		return true;
	}
}
