using System;
using UnityEngine;

public abstract class Option<T> : INameListOption, IOption where T : struct, IConvertible, IComparable
{
	public abstract string Label { get; }

	public abstract OptionsData.Categories Category { get; }

	public string[] GetNames()
	{
		T[] array = Enum.GetValues(typeof(T)) as T[];
		return array.ConvertAll((T x) => "OptionValue." + x);
	}

	public void SetOption(int _value)
	{
		T[] array = Enum.GetValues(typeof(T)) as T[];
		SetState(array[Mathf.Clamp(_value, 0, array.Length - 1)]);
	}

	public int GetOption()
	{
		T[] array = Enum.GetValues(typeof(T)) as T[];
		T state = GetState();
		return array.FindIndex_Predicate((T x) => x.Equals(state));
	}

	protected virtual string ToString(T _value)
	{
		return _value.ToString();
	}

	protected abstract T GetState();

	protected abstract void SetState(T _state);

	public abstract void Commit();
}
