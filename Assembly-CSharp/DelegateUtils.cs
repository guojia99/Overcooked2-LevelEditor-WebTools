using System.Collections.Generic;

public static class DelegateUtils
{
	public static bool CallForResult<T>(this List<Generic<T>> _delegates, T _result)
	{
		for (int i = 0; i < _delegates.Count; i++)
		{
			if (_delegates[i]().Equals(_result))
			{
				return true;
			}
		}
		return false;
	}

	public static bool CallForResult<T, P1>(this List<Generic<T, P1>> _delegates, T _result, P1 _param1)
	{
		for (int i = 0; i < _delegates.Count; i++)
		{
			if (_delegates[i](_param1).Equals(_result))
			{
				return true;
			}
		}
		return false;
	}

	public static bool CallForResult<T, P1, P2>(this List<Generic<T, P1, P2>> _delegates, T _result, P1 _param1, P2 _param2)
	{
		for (int i = 0; i < _delegates.Count; i++)
		{
			if (_delegates[i](_param1, _param2).Equals(_result))
			{
				return true;
			}
		}
		return false;
	}

	public static bool CallForResult<T, P1, P2, P3>(this List<Generic<T, P1, P2, P3>> _delegates, T _result, P1 _param1, P2 _param2, P3 _param3)
	{
		for (int i = 0; i < _delegates.Count; i++)
		{
			if (_delegates[i](_param1, _param2, _param3).Equals(_result))
			{
				return true;
			}
		}
		return false;
	}

	public static bool CallForResult<T, P1, P2, P3, P4>(this List<Generic<T, P1, P2, P3, P4>> _delegates, T _result, P1 _param1, P2 _param2, P3 _param3, P4 _param4)
	{
		for (int i = 0; i < _delegates.Count; i++)
		{
			if (_delegates[i](_param1, _param2, _param3, _param4).Equals(_result))
			{
				return true;
			}
		}
		return false;
	}

	public static bool CallForResult<T, P1, P2, P3, P4, P5>(this List<Generic<T, P1, P2, P3, P4, P5>> _delegates, T _result, P1 _param1, P2 _param2, P3 _param3, P4 _param4, P5 _param5)
	{
		for (int i = 0; i < _delegates.Count; i++)
		{
			if (_delegates[i](_param1, _param2, _param3, _param4, _param5).Equals(_result))
			{
				return true;
			}
		}
		return false;
	}

	public static bool CallForResult<T, P1, P2, P3, P4, P5, P6>(this List<Generic<T, P1, P2, P3, P4, P5, P6>> _delegates, T _result, P1 _param1, P2 _param2, P3 _param3, P4 _param4, P5 _param5, P6 _param6)
	{
		for (int i = 0; i < _delegates.Count; i++)
		{
			if (_delegates[i](_param1, _param2, _param3, _param4, _param5, _param6).Equals(_result))
			{
				return true;
			}
		}
		return false;
	}
}
