using System;
using UnityEngine;

public class TriggerQueue : TimedQueue, ITriggerReceiver
{
	public enum TriggerType
	{
		Object = 0,
		Animator = 1
	}

	[Serializable]
	public class Queue
	{
		[SerializeField]
		public string[] m_triggers = new string[0];

		[SerializeField]
		public float[] m_delays = new float[0];

		[NonSerialized]
		public int[] m_triggerHashs = new int[0];

		public void InitHashs()
		{
			m_triggerHashs = new int[m_triggers.Length];
			for (int i = 0; i < m_triggerHashs.Length; i++)
			{
				m_triggerHashs[i] = Animator.StringToHash(m_triggers[i]);
			}
		}
	}

	[Space]
	[SerializeField]
	public TriggerType m_targetType = TriggerType.Animator;

	[HideInInspectorTest("m_targetType", TriggerType.Animator)]
	[SerializeField]
	public Animator m_animator;

	[HideInInspectorTest("m_targetType", TriggerType.Object)]
	[SerializeField]
	public GameObject m_targetObject;

	[Space]
	[HideInInspectorTest("m_targetType", TriggerType.Animator)]
	[SerializeField]
	[ReadOnly]
	public string m_finishedTrigger = "AnimationFinished";

	[HideInInspectorTest("m_targetType", TriggerType.Animator)]
	[SerializeField]
	public bool m_waitForFinished = true;

	[SerializeField]
	public Queue m_queue = new Queue();

	private CallbackVoid m_finishedCallback = delegate
	{
	};

	protected virtual void Awake()
	{
		m_queue.InitHashs();
	}

	public void RegisterFinishedCallback(CallbackVoid _callback)
	{
		m_finishedCallback = (CallbackVoid)Delegate.Combine(m_finishedCallback, _callback);
	}

	public void DeregisterFinishedCallback(CallbackVoid _callback)
	{
		m_finishedCallback = (CallbackVoid)Delegate.Remove(m_finishedCallback, _callback);
	}

	public void OnTrigger(string _trigger)
	{
		if (_trigger == m_finishedTrigger)
		{
			m_finishedCallback();
		}
	}

	public override float GetQueueLength()
	{
		return m_queue.m_triggers.Length;
	}

	public override float GetDelay(int _index)
	{
		return m_queue.m_delays[_index];
	}
}
