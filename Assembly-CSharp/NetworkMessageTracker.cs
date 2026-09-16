using System.Collections.Generic;
using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class NetworkMessageTracker : DebugDisplay
{
	public enum MessageBatchType
	{
		Reliable = 0,
		Unreliable = 1,
		COUNT = 2
	}

	public class BatchCount
	{
		public float time;

		public int iCount;

		public BatchCount(float _time, int _iCount)
		{
			time = _time;
			iCount = _iCount;
		}
	}

	private FastList<FastList<float>> m_SentGlobalEvents = new FastList<FastList<float>>();

	private FastList<FastList<float>> m_ReceivedGlobalEvents = new FastList<FastList<float>>();

	private FastList<FastList<float>> m_SentEntityUpdates = new FastList<FastList<float>>();

	private FastList<FastList<float>> m_ReceivedEntityUpdates = new FastList<FastList<float>>();

	private FastList<FastList<float>> m_SentEntityEvents = new FastList<FastList<float>>();

	private FastList<FastList<float>> m_ReceivedEntityEvents = new FastList<FastList<float>>();

	private FastList<FastList<BatchCount>> m_SentMessageBatches = new FastList<FastList<BatchCount>>();

	private FastList<FastList<BatchCount>> m_ReceivedMessageBatches = new FastList<FastList<BatchCount>>();

	private FastList<int> m_SentGlobalEventCounts = new FastList<int>(42);

	private FastList<int> m_ReceivedGlobalEventCounts = new FastList<int>(42);

	private FastList<int> m_SentEntityUpdateCounts = new FastList<int>(65);

	private FastList<int> m_ReceivedEntityUpdateCounts = new FastList<int>(65);

	private FastList<int> m_SentEntityEventCounts = new FastList<int>(65);

	private FastList<int> m_ReceivedEntityEventCounts = new FastList<int>(65);

	private FastList<int> m_SentMessageBatchCounts = new FastList<int>(2);

	private FastList<int> m_ReceivedMessageBatchCounts = new FastList<int>(2);

	private FastList<int> m_SentGlobalTotals = new FastList<int>(2);

	private FastList<int> m_ReceivedGlobalTotals = new FastList<int>(2);

	private float m_fNextUpdate;

	private const float kFrameRate = 1f;

	private const float kFrameDelay = 1f;

	private IOnlinePlatformManager m_OnlinePlatformManager;

	public override void OnSetUp()
	{
		InitList(m_SentGlobalEvents, m_SentGlobalEventCounts, 42);
		InitList(m_ReceivedGlobalEvents, m_ReceivedGlobalEventCounts, 42);
		InitList(m_SentEntityUpdates, m_SentEntityUpdateCounts, 65);
		InitList(m_ReceivedEntityUpdates, m_ReceivedEntityUpdateCounts, 65);
		InitList(m_SentEntityEvents, m_SentEntityEventCounts, 65);
		InitList(m_ReceivedEntityEvents, m_ReceivedEntityEventCounts, 65);
		InitList(m_SentMessageBatches, m_SentMessageBatchCounts, 2);
		InitList(m_ReceivedMessageBatches, m_ReceivedMessageBatchCounts, 2);
		m_OnlinePlatformManager = GameUtils.RequireManagerInterface<IOnlinePlatformManager>();
	}

	private void InitList(FastList<FastList<float>> list, FastList<int> counts, int count)
	{
		for (int i = 0; i < count; i++)
		{
			list.Add(new FastList<float>());
			counts.Add(0);
		}
	}

	private void InitList(FastList<FastList<BatchCount>> list, FastList<int> counts, int count)
	{
		for (int i = 0; i < count; i++)
		{
			list.Add(new FastList<BatchCount>());
			counts.Add(0);
		}
	}

	public override void OnUpdate()
	{
		if (m_fNextUpdate < Time.time)
		{
			float time = Time.time;
			RemoveOldEntriesFromList(m_SentGlobalEvents, m_SentGlobalEventCounts, time);
			RemoveOldEntriesFromList(m_ReceivedGlobalEvents, m_ReceivedGlobalEventCounts, time);
			RemoveOldEntriesFromList(m_SentEntityUpdates, m_SentEntityUpdateCounts, time);
			RemoveOldEntriesFromList(m_ReceivedEntityUpdates, m_ReceivedEntityUpdateCounts, time);
			RemoveOldEntriesFromList(m_SentEntityEvents, m_SentEntityEventCounts, time);
			RemoveOldEntriesFromList(m_ReceivedEntityEvents, m_ReceivedEntityEventCounts, time);
			RemoveOldEntriesFromList(m_SentMessageBatches, m_SentMessageBatchCounts, time);
			RemoveOldEntriesFromList(m_ReceivedMessageBatches, m_ReceivedMessageBatchCounts, time);
			m_SentGlobalTotals._items[0] = GetTotal(m_SentMessageBatches._items[0]);
			m_ReceivedGlobalTotals._items[0] = GetTotal(m_ReceivedMessageBatches._items[0]);
			m_SentGlobalTotals._items[1] = GetTotal(m_SentMessageBatches._items[1]);
			m_ReceivedGlobalTotals._items[1] = GetTotal(m_ReceivedMessageBatches._items[1]);
			m_fNextUpdate = Time.time + 1f;
		}
	}

	private int GetTotal(FastList<BatchCount> eventList)
	{
		int num = 0;
		int count = eventList.Count;
		for (int i = 0; i < count; i++)
		{
			num += eventList._items[i].iCount;
		}
		return num;
	}

	private void RemoveOldEntriesFromList(FastList<FastList<float>> list, FastList<int> counts, float fCurrentTime)
	{
		int count = list.Count;
		for (int i = 0; i < count; i++)
		{
			FastList<float> fastList = list._items[i];
			fastList.RemoveAll((float x) => x + 1f < fCurrentTime);
			counts._items[i] = fastList.Count;
		}
	}

	private void RemoveOldEntriesFromList(FastList<FastList<BatchCount>> list, FastList<int> counts, float fCurrentTime)
	{
		int count = list.Count;
		for (int i = 0; i < count; i++)
		{
			FastList<BatchCount> fastList = list._items[i];
			fastList.RemoveAll((BatchCount x) => x.time + 1f < fCurrentTime);
			counts._items[i] = fastList.Count;
		}
	}

	public override void OnDraw(ref Rect rect, GUIStyle style)
	{
		IOnlineMultiplayerTransportStats onlineMultiplayerTransportStats = null;
		if (m_OnlinePlatformManager != null)
		{
			onlineMultiplayerTransportStats = m_OnlinePlatformManager.OnlineMultiplayerTransportStats();
		}
		if (onlineMultiplayerTransportStats != null)
		{
			DrawText(ref rect, style, onlineMultiplayerTransportStats.Text);
		}
		AppendSentAndReceivedTotalsText(m_SentGlobalTotals, m_ReceivedGlobalTotals, m_SentMessageBatchCounts, m_ReceivedMessageBatchCounts, "Network sends/receives per second", delegate(int x)
		{
			MessageBatchType messageBatchType = (MessageBatchType)x;
			return messageBatchType.ToString();
		}, ref rect, style);
		AppendSentAndReceivedText(m_SentGlobalEventCounts, m_ReceivedGlobalEventCounts, "Game messages per second", delegate(int x)
		{
			MessageType messageType = (MessageType)x;
			return messageType.ToString();
		}, ref rect, style);
		AppendSentAndReceivedText(m_SentEntityUpdateCounts, m_ReceivedEntityUpdateCounts, "EntitySynchronisation breakdown", (int x) => ((EntityType)x/*cast due to .constrained prefix*/).ToString(), ref rect, style);
		AppendSentAndReceivedText(m_SentEntityEventCounts, m_ReceivedEntityEventCounts, "EntityEvent breakdown", (int x) => ((EntityType)x/*cast due to .constrained prefix*/).ToString(), ref rect, style);
	}

	private void AppendSentAndReceivedTotalsText(FastList<int> sentTotals, FastList<int> receivedTotals, FastList<int> sentList, FastList<int> receivedList, string title, Generic<string, int> toString, ref Rect rect, GUIStyle style)
	{
		int count = sentList.Count;
		DrawText(ref rect, style, title);
		for (int i = 0; i < count; i++)
		{
			int num = sentTotals._items[i];
			int num2 = receivedTotals._items[i];
			int num3 = sentList._items[i];
			int num4 = receivedList._items[i];
			DrawText(ref rect, style, toString(i) + ": sent " + num.ToString("0000") + " in " + num3.ToString("0000") + " batches");
			DrawText(ref rect, style, toString(i) + ": received " + num2.ToString("0000") + " in " + num4.ToString("0000") + " batches");
		}
	}

	private void AppendSentAndReceivedText(FastList<int> sentList, FastList<int> receivedList, string title, Generic<string, int> toString, ref Rect rect, GUIStyle style)
	{
		int count = sentList.Count;
		bool flag = false;
		for (int i = 0; i < count; i++)
		{
			int num = sentList._items[i];
			int num2 = receivedList._items[i];
			if (num > 0 || num2 > 0)
			{
				if (!flag)
				{
					DrawText(ref rect, style, title);
					flag = true;
				}
				DrawText(ref rect, style, toString(i) + " Snt: " + num.ToString("0000") + " Rcvd: " + num2.ToString("0000"));
			}
		}
	}

	public void TrackSentGlobalEvent(MessageType type)
	{
		m_SentGlobalEvents._items[(int)type].Add(Time.time);
	}

	public void TrackReceivedGlobalEvent(MessageType type)
	{
		m_ReceivedGlobalEvents._items[(int)type].Add(Time.time);
	}

	public void TrackSentEntityUpdate(EntityType entity)
	{
		m_SentEntityUpdates._items[(uint)entity].Add(Time.time);
	}

	public void TrackReceivedEntityUpdate(EntityType entity)
	{
		m_ReceivedEntityUpdates._items[(uint)entity].Add(Time.time);
	}

	public void TrackSentEntityEvent(EntityType entity)
	{
		m_SentEntityEvents._items[(uint)entity].Add(Time.time);
	}

	public void TrackReceivedEntityEvent(EntityType entity)
	{
		m_ReceivedEntityEvents._items[(uint)entity].Add(Time.time);
	}

	public void TrackSentMessageBatch(MessageBatchType type, int iMessages)
	{
		m_SentMessageBatches._items[(int)type].Add(new BatchCount(Time.time, iMessages));
	}

	public void TrackReceivedMessageBatch(MessageBatchType type, int iMessages)
	{
		m_ReceivedMessageBatches._items[(int)type].Add(new BatchCount(Time.time, iMessages));
	}
}
