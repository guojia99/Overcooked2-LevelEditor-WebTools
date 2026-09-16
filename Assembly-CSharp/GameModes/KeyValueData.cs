using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameModes
{
	[Serializable]
	public abstract class KeyValueData<K, V> : ScriptableObject
	{
		[SerializeField]
		protected V m_defaultValue = default(V);

		[SerializeField]
		protected List<K> m_keys = new List<K>();

		[SerializeField]
		protected List<V> m_values = new List<V>();

		public virtual bool Contains(K key)
		{
			return IndexOf(key) != -1;
		}

		protected virtual int IndexOf(K key)
		{
			return m_keys.IndexOf(key);
		}

		public virtual V Get(K key)
		{
			int num = IndexOf(key);
			return (num == -1) ? m_defaultValue : m_values[num];
		}
	}
}
