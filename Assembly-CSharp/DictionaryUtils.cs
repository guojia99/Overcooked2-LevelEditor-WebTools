using System;
using System.Collections.Generic;

public static class DictionaryUtils
{
	public static Dictionary<K, U> ConvertValues<K, U, T>(this Dictionary<K, T> _input, Converter<T, U> _converter)
	{
		Dictionary<K, U> dictionary = new Dictionary<K, U>();
		foreach (KeyValuePair<K, T> item in _input)
		{
			dictionary.Add(item.Key, _converter(item.Value));
		}
		return dictionary;
	}

	public static void RemoveAll<K, T>(this Dictionary<K, T> _input, Generic<bool, KeyValuePair<K, T>> _shouldRemove)
	{
		List<K> list = new List<K>();
		foreach (KeyValuePair<K, T> item in _input)
		{
			if (_shouldRemove(item))
			{
				list.Add(item.Key);
			}
		}
		for (int i = 0; i < list.Count; i++)
		{
			_input.Remove(list[i]);
		}
	}

	public static V CreationGet<K, V>(this Dictionary<K, V> _input, K _key) where V : new()
	{
		if (!_input.ContainsKey(_key))
		{
			_input.Add(_key, new V());
		}
		return _input[_key];
	}

	public static V CreationGet<K, V>(this Dictionary<K, V> _input, K _key, V _default = default(V))
	{
		if (!_input.ContainsKey(_key))
		{
			_input.Add(_key, _default);
		}
		return _input[_key];
	}

	public static V SafeGet<K, V>(this Dictionary<K, V> _input, K _key, V _default = default(V))
	{
		if (_input.ContainsKey(_key))
		{
			return _input[_key];
		}
		return _default;
	}

	public static void SafeAdd<K, V>(this Dictionary<K, V> _input, K _key, V _value)
	{
		if (_input.ContainsKey(_key))
		{
			_input[_key] = _value;
		}
		else
		{
			_input.Add(_key, _value);
		}
	}

	public static void SafeRemove<K, V>(this Dictionary<K, V> _input, K _key)
	{
		if (_input.ContainsKey(_key))
		{
			_input.Remove(_key);
		}
	}
}
