namespace System.Collections.Generic
{
	[Serializable]
	public class FastList<T>
	{
		private const int _defaultCapacity = 4;

		public T[] _items;

		private int _size;

		[NonSerialized]
		private object _syncRoot;

		private static readonly T[] _emptyArray = new T[0];

		public int Capacity
		{
			get
			{
				return _items.Length;
			}
			set
			{
				if (value < _size)
				{
					value = _size;
				}
				if (value == _items.Length)
				{
					return;
				}
				if (value > 0)
				{
					T[] array = new T[value];
					if (_size > 0)
					{
						Array.Copy(_items, 0, array, 0, _size);
					}
					_items = array;
				}
				else
				{
					_items = _emptyArray;
				}
			}
		}

		public int Count
		{
			get
			{
				return _size;
			}
		}

		public FastList()
		{
			_items = _emptyArray;
		}

		public FastList(int capacity)
		{
			if (capacity < 0)
			{
				capacity = 0;
			}
			if (capacity == 0)
			{
				_items = _emptyArray;
			}
			else
			{
				_items = new T[capacity];
			}
		}

		public FastList(IEnumerable<T> collection)
		{
			ICollection<T> collection2 = null;
			if (collection != null)
			{
				collection2 = collection as ICollection<T>;
			}
			if (collection2 != null)
			{
				int count = collection2.Count;
				if (count == 0)
				{
					_items = _emptyArray;
					return;
				}
				_items = new T[count];
				collection2.CopyTo(_items, 0);
				_size = count;
				return;
			}
			_size = 0;
			_items = _emptyArray;
			if (collection == null)
			{
				return;
			}
			foreach (T item in collection)
			{
				Add(item);
			}
		}

		private static bool IsCompatibleObject(object value)
		{
			return value is T || (value == null && default(T) == null);
		}

		public void Add(T item)
		{
			if (_size == _items.Length)
			{
				EnsureCapacity(_size + 1);
			}
			_items[_size++] = item;
		}

		public void AddRange(IEnumerable<T> collection)
		{
			InsertRange(_size, collection);
		}

		public int BinarySearch(int index, int count, T item, IComparer<T> comparer)
		{
			if (index < 0)
			{
				index = 0;
			}
			if (count < 0)
			{
				count = 0;
			}
			if (_size - index < count)
			{
				return -1;
			}
			return Array.BinarySearch(_items, index, count, item, comparer);
		}

		public int BinarySearch(T item)
		{
			return BinarySearch(0, Count, item, null);
		}

		public int BinarySearch(T item, IComparer<T> comparer)
		{
			return BinarySearch(0, Count, item, comparer);
		}

		public void Clear()
		{
			if (_size > 0)
			{
				Array.Clear(_items, 0, _size);
				_size = 0;
			}
		}

		public bool Contains(T item)
		{
			return _size != 0 && IndexOf(item) != -1;
		}

		public void CopyTo(T[] array)
		{
			CopyTo(array, 0);
		}

		public void CopyTo(int index, T[] array, int arrayIndex, int count)
		{
			if (_size - index < count)
			{
			}
			Array.Copy(_items, index, array, arrayIndex, count);
		}

		public void CopyTo(T[] array, int arrayIndex)
		{
			Array.Copy(_items, 0, array, arrayIndex, _size);
		}

		private void EnsureCapacity(int min)
		{
			if (_items.Length < min)
			{
				int num = ((_items.Length != 0) ? (_items.Length * 2) : 4);
				if (num < min)
				{
					num = min;
				}
				Capacity = num;
			}
		}

		public bool Exists(Predicate<T> match)
		{
			return FindIndex(match) != -1;
		}

		public T Find(Predicate<T> match)
		{
			if (match == null)
			{
			}
			for (int i = 0; i < _size; i++)
			{
				if (match(_items[i]))
				{
					return _items[i];
				}
			}
			return default(T);
		}

		public FastList<T> FindAll(Predicate<T> match)
		{
			if (match == null)
			{
			}
			FastList<T> fastList = new FastList<T>();
			for (int i = 0; i < _size; i++)
			{
				if (match(_items[i]))
				{
					fastList.Add(_items[i]);
				}
			}
			return fastList;
		}

		public int FindIndex(Predicate<T> match)
		{
			return FindIndex(0, _size, match);
		}

		public int FindIndex(int startIndex, Predicate<T> match)
		{
			return FindIndex(startIndex, _size - startIndex, match);
		}

		public int FindIndex(int startIndex, int count, Predicate<T> match)
		{
			if ((uint)startIndex > (uint)_size)
			{
			}
			if (count < 0 || startIndex > _size - count)
			{
			}
			if (match == null)
			{
			}
			int num = startIndex + count;
			for (int i = startIndex; i < num; i++)
			{
				if (match(_items[i]))
				{
					return i;
				}
			}
			return -1;
		}

		public T FindLast(Predicate<T> match)
		{
			if (match == null)
			{
			}
			for (int num = _size - 1; num >= 0; num--)
			{
				if (match(_items[num]))
				{
					return _items[num];
				}
			}
			return default(T);
		}

		public int FindLastIndex(Predicate<T> match)
		{
			return FindLastIndex(_size - 1, _size, match);
		}

		public int FindLastIndex(int startIndex, Predicate<T> match)
		{
			return FindLastIndex(startIndex, startIndex + 1, match);
		}

		public int FindLastIndex(int startIndex, int count, Predicate<T> match)
		{
			if (match == null)
			{
			}
			if (_size == 0)
			{
				if (startIndex == -1)
				{
				}
			}
			else if ((uint)startIndex < (uint)_size)
			{
			}
			if (count < 0 || startIndex - count + 1 < 0)
			{
			}
			int num = startIndex - count;
			for (int num2 = startIndex; num2 > num; num2--)
			{
				if (match(_items[num2]))
				{
					return num2;
				}
			}
			return -1;
		}

		public void ForEach(Action<T> action)
		{
			if (action == null)
			{
			}
			for (int i = 0; i < _size; i++)
			{
				action(_items[i]);
			}
		}

		public FastList<T> GetRange(int index, int count)
		{
			if (index < 0)
			{
			}
			if (count < 0)
			{
			}
			if (_size - index < count)
			{
			}
			FastList<T> fastList = new FastList<T>(count);
			Array.Copy(_items, index, fastList._items, 0, count);
			fastList._size = count;
			return fastList;
		}

		public int IndexOf(T item)
		{
			return Array.IndexOf(_items, item, 0, _size);
		}

		public int IndexOf(T item, int index)
		{
			if (index > _size)
			{
			}
			return Array.IndexOf(_items, item, index, _size - index);
		}

		public int IndexOf(T item, int index, int count)
		{
			if (index > _size)
			{
			}
			return Array.IndexOf(_items, item, index, count);
		}

		public void Insert(int index, T item)
		{
			if ((uint)index > (uint)_size)
			{
			}
			if (_size == _items.Length)
			{
				EnsureCapacity(_size + 1);
			}
			if (index < _size)
			{
				Array.Copy(_items, index, _items, index + 1, _size - index);
			}
			_items[index] = item;
			_size++;
		}

		public void InsertRange(int index, IEnumerable<T> collection)
		{
			if (collection == null)
			{
			}
			if ((uint)index > (uint)_size)
			{
			}
			ICollection<T> collection2 = collection as ICollection<T>;
			if (collection2 != null)
			{
				int count = collection2.Count;
				if (count > 0)
				{
					EnsureCapacity(_size + count);
					if (index < _size)
					{
						Array.Copy(_items, index, _items, index + count, _size - index);
					}
					if (this == collection2)
					{
						Array.Copy(_items, 0, _items, index, index);
						Array.Copy(_items, index + count, _items, index * 2, _size - index);
					}
					else
					{
						collection2.CopyTo(_items, index);
					}
					_size += count;
				}
				return;
			}
			using (IEnumerator<T> enumerator = collection.GetEnumerator())
			{
				while (enumerator.MoveNext())
				{
					Insert(index++, enumerator.Current);
				}
			}
		}

		public int LastIndexOf(T item)
		{
			if (_size == 0)
			{
				return -1;
			}
			return LastIndexOf(item, _size - 1, _size);
		}

		public int LastIndexOf(T item, int index)
		{
			if (index >= _size)
			{
			}
			return LastIndexOf(item, index, index + 1);
		}

		public int LastIndexOf(T item, int index, int count)
		{
			if (Count == 0 || index < 0)
			{
			}
			if (Count == 0 || count < 0)
			{
			}
			if (_size == 0)
			{
				return -1;
			}
			if (index >= _size)
			{
			}
			if (count > index + 1)
			{
			}
			return Array.LastIndexOf(_items, item, index, count);
		}

		public bool Remove(T item)
		{
			int num = IndexOf(item);
			if (num >= 0)
			{
				RemoveAt(num);
				return true;
			}
			return false;
		}

		public int RemoveAll(Predicate<T> match)
		{
			if (match == null)
			{
			}
			int i;
			for (i = 0; i < _size && !match(_items[i]); i++)
			{
			}
			if (i >= _size)
			{
				return 0;
			}
			int j = i + 1;
			while (j < _size)
			{
				for (; j < _size && match(_items[j]); j++)
				{
				}
				if (j < _size)
				{
					_items[i++] = _items[j++];
				}
			}
			Array.Clear(_items, i, _size - i);
			int result = _size - i;
			_size = i;
			return result;
		}

		public void CyclicRemoveAt(int index)
		{
			_items[index] = _items[_size--];
		}

		public void RemoveAt(int index)
		{
			if ((uint)index >= (uint)_size)
			{
			}
			_size--;
			if (index < _size)
			{
				Array.Copy(_items, index + 1, _items, index, _size - index);
			}
			_items[_size] = default(T);
		}

		public void RemoveRange(int index, int count)
		{
			if (index < 0)
			{
			}
			if (count < 0)
			{
			}
			if (_size - index < count)
			{
			}
			if (count > 0)
			{
				_size -= count;
				if (index < _size)
				{
					Array.Copy(_items, index + count, _items, index, _size - index);
				}
				Array.Clear(_items, _size, count);
			}
		}

		public void Reverse()
		{
			Reverse(0, Count);
		}

		public void Reverse(int index, int count)
		{
			if (index < 0)
			{
			}
			if (count < 0)
			{
			}
			if (_size - index < count)
			{
			}
			Array.Reverse(_items, index, count);
		}

		public void Sort()
		{
			Sort(0, Count, null);
		}

		public void Sort(IComparer<T> comparer)
		{
			Sort(0, Count, comparer);
		}

		public void Sort(int index, int count, IComparer<T> comparer)
		{
			if (index < 0)
			{
			}
			if (count < 0)
			{
			}
			if (_size - index < count)
			{
			}
			Array.Sort(_items, index, count, comparer);
		}

		public T[] ToArray()
		{
			T[] array = new T[_size];
			Array.Copy(_items, 0, array, 0, _size);
			return array;
		}

		public void TrimExcess()
		{
			int num = (int)((double)_items.Length * 0.9);
			if (_size < num)
			{
				Capacity = _size;
			}
		}

		public bool TrueForAll(Predicate<T> match)
		{
			if (match == null)
			{
			}
			for (int i = 0; i < _size; i++)
			{
				if (!match(_items[i]))
				{
					return false;
				}
			}
			return true;
		}
	}
}
