using System;
using System.Collections.Generic;
using UnityEngine;

public static class ArrayUtils
{
	public static bool IsEmpty<T>(this T[] _array)
	{
		return _array.Length == 0;
	}

	public static void SetValues<T>(this T[] _array, T _value, int _start, int _end)
	{
		for (int i = _start; i < _end; i++)
		{
			_array[i] = _value;
		}
	}

	public static T[] Union<T>(this T[] _a, T[] _b)
	{
		T[] array = new T[_a.Length + _b.Length];
		_a.CopyTo(array, 0);
		_b.CopyTo(array, _a.Length);
		return array;
	}

	public static T[] Compliment<T>(this T[] _a, T[] _b)
	{
		Predicate<T> match = (T _v) => !_b.Contains(_v);
		return _a.FindAll(match);
	}

	public static void ExpandingAssign<T>(ref T[] _array, int _index, T _value)
	{
		if (_index >= _array.Length)
		{
			Array.Resize(ref _array, _index + 1);
		}
		_array[_index] = _value;
	}

	public static T TryAtIndex<T>(this T[] _array, int _index)
	{
		return _array.TryAtIndex(_index, default(T));
	}

	public static T TryAtIndex<T>(this T[] _array, int _index, T _default)
	{
		if (_index >= _array.Length || _index < 0)
		{
			return _default;
		}
		return _array[_index];
	}

	public static void SafeSet<T>(this T[] _array, int _index, T _value)
	{
		if (_index >= 0 && _index < _array.Length)
		{
			_array[_index] = _value;
		}
	}

	public static U[] ConvertAll<T, U>(this T[] _array, Converter<T, U> _converter)
	{
		return Array.ConvertAll(_array, _converter);
	}

	public static U[] ConvertAll<T, U>(this T[] _array, Generic<U, int, T> _converter)
	{
		U[] array = new U[_array.Length];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = _converter(i, _array[i]);
		}
		return array;
	}

	public static U[] ConvertRange<T, U>(this T[] _array, Generic<U, int, T> _converter, int _start, int _length)
	{
		U[] array = new U[_length];
		for (int i = 0; i < _length; i++)
		{
			int num = i + _start;
			array[i] = _converter(num, _array[num]);
		}
		return array;
	}

	public static T[] Intersection<T>(this T[] _a, T[] _b) where T : IComparable, IConvertible
	{
		T[] array = new T[0];
		bool[] array2 = new bool[_b.Length];
		for (int i = 0; i < _a.Length; i++)
		{
			for (int j = 0; j < _b.Length; j++)
			{
				if (!array2[j] && _b[j].Equals(_a[i]))
				{
					array2[j] = true;
					Array.Resize(ref array, array.Length + 1);
					array[array.Length - 1] = _a[i];
					break;
				}
			}
		}
		return array;
	}

	public static bool Contains<T>(this T[] _array, T _value)
	{
		return _array.FindIndex_Predicate((T x) => _value.Equals(x)) != -1;
	}

	public static bool Contains<T>(this T[] _array, Predicate<T> _matchFunction)
	{
		return _array.FindIndex_Predicate(_matchFunction) != -1;
	}

	public static bool Contains<T>(this T[] _array, Generic<bool, int, T> _matchFunction)
	{
		return _array.FindIndex_Generic(_matchFunction) != -1;
	}

	public static bool Contains<T>(this T[] _array, T[] _subArray)
	{
		bool[] used = new bool[_array.Length];
		for (int i = 0; i < _subArray.Length; i++)
		{
			int num = _array.FindIndex_Generic((int j, T x) => !used[j] && _subArray[i].Equals(x));
			if (num == -1)
			{
				return false;
			}
			used[num] = true;
		}
		return true;
	}

	public static int FindIndex<T>(this T[] _array, T _value) where T : IComparable
	{
		for (int i = 0; i < _array.Length; i++)
		{
			if (_value.Equals(_array[i]))
			{
				return i;
			}
		}
		return -1;
	}

	public static int FindIndex_Predicate<T>(this T[] _array, Predicate<T> _matchFunction)
	{
		return Array.FindIndex(_array, _matchFunction);
	}

	public static int FindIndex_Generic<T>(this T[] _array, Generic<bool, int, T> _matchFunction)
	{
		for (int i = 0; i < _array.Length; i++)
		{
			if (_matchFunction(i, _array[i]))
			{
				return i;
			}
		}
		return -1;
	}

	public static void PushBack<T>(ref T[] _array, T _value)
	{
		int num = _array.Length;
		Array.Resize(ref _array, num + 1);
		_array[num] = _value;
	}

	public static void Insert<T>(ref T[] _array, int _index, T _value)
	{
		int num = _array.Length;
		Array.Resize(ref _array, 1 + num);
		for (int num2 = num; num2 > _index; num2--)
		{
			_array[num2] = _array[num2 - 1];
		}
		_array[_index] = _value;
	}

	public static void RemoveAllDuplicates<T>(ref T[] _array)
	{
		for (int num = _array.Length - 1; num >= 0; num--)
		{
			T e = _array[num];
			if (_array.FindAll((T x) => x.Equals(e)).Length > 1)
			{
				RemoveAt(ref _array, num);
			}
		}
	}

	public static void RemoveAt<T>(ref T[] _array, int _index)
	{
		int num = _array.Length;
		for (int i = _index + 1; i < num; i++)
		{
			_array[i - 1] = _array[i];
		}
		Array.Resize(ref _array, num - 1);
	}

	public static KeyValuePair<int, T> FindLowestScoring<T>(this T[] _array, Generic<float, T> _scoreFunction)
	{
		float o_score;
		return _array.FindLowestScoring(_scoreFunction, out o_score);
	}

	public static KeyValuePair<int, T> FindLowestScoring<T>(this T[] _array, Generic<float, T> _scoreFunction, out float o_score)
	{
		o_score = float.MaxValue;
		int num = -1;
		for (int i = 0; i < _array.Length; i++)
		{
			float num2 = _scoreFunction(_array[i]);
			if (num2 < o_score)
			{
				o_score = num2;
				num = i;
			}
		}
		if (num == -1)
		{
			return new KeyValuePair<int, T>(num, default(T));
		}
		return new KeyValuePair<int, T>(num, _array[num]);
	}

	public static KeyValuePair<int, T> FindHighestScoring<T>(this T[] _array, Generic<float, T> _scoreFunction)
	{
		float o_score;
		return _array.FindHighestScoring(_scoreFunction, out o_score);
	}

	public static KeyValuePair<int, T> FindHighestScoring<T>(this T[] _array, Generic<float, T> _scoreFunction, out float o_score)
	{
		o_score = float.MinValue;
		int num = -1;
		for (int i = 0; i < _array.Length; i++)
		{
			float num2 = _scoreFunction(_array[i]);
			if (num2 > o_score)
			{
				o_score = num2;
				num = i;
			}
		}
		if (num == -1)
		{
			return new KeyValuePair<int, T>(num, default(T));
		}
		return new KeyValuePair<int, T>(num, _array[num]);
	}

	public static T GetRandomElement<T>(this T[] _items)
	{
		if (_items.Length > 0)
		{
			int num = UnityEngine.Random.Range(0, _items.Length);
			return _items[num];
		}
		return default(T);
	}

	public static KeyValuePair<int, T> GetWeightedRandomElement<T>(this T[] _items, Generic<float, int, T> _weight)
	{
		float num = 0f;
		for (int i = 0; i < _items.Length; i++)
		{
			float num2 = _weight(i, _items[i]);
			num += num2;
		}
		float num3 = UnityEngine.Random.Range(0f, num);
		float num4 = 0f;
		for (int j = 0; j < _items.Length; j++)
		{
			num4 += _weight(j, _items[j]);
			if (num3 <= num4)
			{
				return new KeyValuePair<int, T>(j, _items[j]);
			}
		}
		return new KeyValuePair<int, T>(-1, default(T));
	}

	public static KeyValuePair<int, T> GetWeightedRandomElement<T>(this T[] _items) where T : IWeight
	{
		return _items.GetWeightedRandomElement((int i, T t) => t.Weight);
	}

	public static T[] FindAll<T>(this T[] _array, Predicate<T> _match)
	{
		T[] _array2 = new T[0];
		for (int i = 0; i < _array.Length; i++)
		{
			if (_match(_array[i]))
			{
				PushBack(ref _array2, _array[i]);
			}
		}
		return _array2;
	}

	public static T[] AllRemoved_Generic<T>(this T[] _array, Generic<bool, int, T> _match)
	{
		List<T> list = new List<T>(_array);
		for (int num = list.Count - 1; num >= 0; num--)
		{
			if (_match(num, list[num]))
			{
				list.RemoveAt(num);
			}
		}
		return list.ToArray();
	}

	public static T[] AllRemoved_Predicate<T>(this T[] _array, Predicate<T> _match)
	{
		List<T> list = new List<T>(_array);
		list.RemoveAll(_match);
		return list.ToArray();
	}

	public static string Concatenated<T>(this T[] _array, Generic<string, int, T> _converter)
	{
		string text = string.Empty;
		for (int i = 0; i < _array.Length; i++)
		{
			text += _converter(i, _array[i]);
		}
		return text;
	}

	public static Vector3 Mean<T>(this T[] _array, Generic<Vector3, T> _converter)
	{
		Vector3 zero = Vector3.zero;
		if (_array.Length > 0)
		{
			for (int i = 0; i < _array.Length; i++)
			{
				zero += _converter(_array[i]);
			}
			return zero / _array.Length;
		}
		return zero;
	}

	public static U Collapse<T, U>(this T[] _array, Generic<U, T, U> _converter)
	{
		U val = default(U);
		for (int i = 0; i < _array.Length; i++)
		{
			val = _converter(_array[i], val);
		}
		return val;
	}

	public static T[] Shuffled<T>(this T[] _array)
	{
		T[] array = new T[_array.Length];
		_array.CopyTo(array, 0);
		array.ShuffleContents();
		return array;
	}

	public static void ShuffleContents<T>(this T[] _array)
	{
		for (int i = 0; i < _array.Length; i++)
		{
			int num = UnityEngine.Random.Range(0, _array.Length);
			if (num != i)
			{
				T val = _array[i];
				_array[i] = _array[num];
				_array[num] = val;
			}
		}
	}

	public static string Stringify<T>(this T[] _array)
	{
		string text = "{ ";
		text = text + _array.Collapse<T, string>(AssembleString) + " }";
		return text + " }";
	}

	public static string AssembleString<T>(T _v, string _s)
	{
		return _s + ", " + _v.ToString();
	}
}
