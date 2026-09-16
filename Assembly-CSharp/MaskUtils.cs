using System;
using System.Collections.Generic;

public static class MaskUtils
{
	public static T[] ConvertToArray<T>(int _mask) where T : struct, IConvertible
	{
		int maxFlag = Enum.GetNames(typeof(T)).Length;
		List<int> list = ConvertToIds(_mask, maxFlag);
		T[] array = new T[list.Count];
		for (int i = 0; i < list.Count; i++)
		{
			array[i] = (T)Enum.ToObject(typeof(T), list[i]);
		}
		return array;
	}

	public static int[] ConvertToIdArray(int _mask, int _maxFlag)
	{
		List<int> list = ConvertToIds(_mask, _maxFlag);
		return list.ToArray();
	}

	private static List<int> ConvertToIds(int _mask, int _maxFlag)
	{
		List<int> list = new List<int>();
		for (int i = 0; i < _maxFlag; i++)
		{
			if ((_mask & (1 << i)) != 0)
			{
				list.Add(i);
			}
		}
		return list;
	}

	public static bool HasFlag<T>(int _mask, T _flag) where T : struct, IConvertible
	{
		int num = 1 << Convert.ToInt32(_flag);
		return (_mask & num) != 0;
	}
}
