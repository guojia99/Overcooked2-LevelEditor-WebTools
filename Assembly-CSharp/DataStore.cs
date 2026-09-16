using System;
using System.Collections.Generic;

public class DataStore : Manager
{
	public struct Id : IEquatable<Id>
	{
		public static readonly Id Invalid = default(Id);

		private const int InvalidId = 0;

		private int m_id;

		public Id(int id)
		{
			m_id = id;
		}

		public Id(object id)
		{
			m_id = id.GetHashCode();
		}

		public override int GetHashCode()
		{
			return m_id;
		}

		public override bool Equals(object obj)
		{
			return m_id == ((Id)obj).m_id;
		}

		public bool Equals(Id other)
		{
			return m_id == other.m_id;
		}

		public override string ToString()
		{
			return "Id(" + m_id + ")";
		}

		public static bool operator ==(Id lhs, Id rhs)
		{
			return lhs.m_id == rhs.m_id;
		}

		public static bool operator !=(Id lhs, Id rhs)
		{
			return lhs.m_id != rhs.m_id;
		}

		public bool IsValid()
		{
			return m_id != 0;
		}
	}

	public delegate void OnChangeNotification(Id id, object data);

	private Dictionary<Id, object> m_data;

	private Dictionary<Id, FastList<OnChangeNotification>> m_listeners;

	private void Awake()
	{
		m_data = new Dictionary<Id, object>(32);
		m_listeners = new Dictionary<Id, FastList<OnChangeNotification>>(32);
	}

	private void OnDestroy()
	{
		Purge();
	}

	public void Purge()
	{
		m_data.Clear();
		m_listeners.Clear();
	}

	public void Write(Id id, object data)
	{
		m_data[id] = data;
		Emit(id, data);
	}

	private void Emit(Id id, object data)
	{
		if (m_listeners.ContainsKey(id))
		{
			FastList<OnChangeNotification> fastList = m_listeners[id];
			for (int i = 0; i < fastList.Count; i++)
			{
				fastList._items[i](id, data);
			}
		}
	}

	public void Register(Id id, OnChangeNotification listener)
	{
		if (!m_listeners.ContainsKey(id))
		{
			m_listeners[id] = new FastList<OnChangeNotification>(8);
		}
		m_listeners[id].Add(listener);
	}

	public void Unregister(Id id, OnChangeNotification listener)
	{
		if (m_listeners.ContainsKey(id))
		{
			m_listeners[id].Remove(listener);
		}
	}
}
