using System;
using System.Collections.Generic;
using System.IO;

namespace InControl
{
	public struct KeyCombo
	{
		private int size;

		private ulong data;

		private static Dictionary<ulong, string> cachedStrings = new Dictionary<ulong, string>();

		public int Count
		{
			get
			{
				return size;
			}
		}

		public bool IsPressed
		{
			get
			{
				if (size == 0)
				{
					return false;
				}
				bool flag = true;
				for (int i = 0; i < size; i++)
				{
					int num = GetInt(i);
					flag = flag && KeyInfo.KeyList[num].IsPressed;
					if (flag)
					{
						int num2 = 1;
					}
				}
				return flag;
			}
		}

		public KeyCombo(params Key[] keys)
		{
			data = 0uL;
			size = 0;
			for (int i = 0; i < keys.Length; i++)
			{
				Add(keys[i]);
			}
		}

		private void AddInt(int key)
		{
			if (size != 8)
			{
				data |= ((ulong)key & 0xFFuL) << size * 8;
				size++;
			}
		}

		private int GetInt(int index)
		{
			return (int)((data >> index * 8) & 0xFF);
		}

		public void Add(Key key)
		{
			AddInt((int)key);
		}

		public Key Get(int index)
		{
			if (index < 0 || index >= size)
			{
				throw new IndexOutOfRangeException("Index " + index + " is out of the range 0.." + size);
			}
			return (Key)GetInt(index);
		}

		public void Clear()
		{
			data = 0uL;
			size = 0;
		}

		public static KeyCombo Detect(bool modifiersAsKeys)
		{
			KeyCombo result = default(KeyCombo);
			if (modifiersAsKeys)
			{
				for (int i = 1; i < 5; i++)
				{
					if (KeyInfo.KeyList[i].IsPressed)
					{
						result.AddInt(i);
					}
				}
			}
			else
			{
				for (int j = 5; j < 13; j++)
				{
					if (KeyInfo.KeyList[j].IsPressed)
					{
						result.AddInt(j);
						return result;
					}
				}
			}
			for (int k = 13; k < KeyInfo.KeyList.Length; k++)
			{
				if (KeyInfo.KeyList[k].IsPressed)
				{
					result.AddInt(k);
					return result;
				}
			}
			result.Clear();
			return result;
		}

		public override string ToString()
		{
			string value;
			if (!cachedStrings.TryGetValue(data, out value))
			{
				value = string.Empty;
				for (int i = 0; i < size; i++)
				{
					if (i != 0)
					{
						value += " ";
					}
					int num = GetInt(i);
					value += KeyInfo.KeyList[num].Name;
				}
			}
			return value;
		}

		public static bool operator ==(KeyCombo a, KeyCombo b)
		{
			return a.data == b.data;
		}

		public static bool operator !=(KeyCombo a, KeyCombo b)
		{
			return a.data != b.data;
		}

		public override bool Equals(object other)
		{
			if (other is KeyCombo)
			{
				KeyCombo keyCombo = (KeyCombo)other;
				return data == keyCombo.data;
			}
			return false;
		}

		public override int GetHashCode()
		{
			return data.GetHashCode();
		}

		internal void Load(BinaryReader reader)
		{
			size = reader.ReadInt32();
			data = reader.ReadUInt64();
		}

		internal void Save(BinaryWriter writer)
		{
			writer.Write(size);
			writer.Write(data);
		}
	}
}
