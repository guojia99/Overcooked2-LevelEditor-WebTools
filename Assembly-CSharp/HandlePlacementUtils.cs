using UnityEngine;

public static class HandlePlacementUtils
{
	public static T GetHighestPriority<T>(T[] _iHandlePlacements) where T : IBaseHandlePlacement
	{
		T val = default(T);
		for (int i = 0; i < _iHandlePlacements.Length; i++)
		{
			T val2 = _iHandlePlacements[i];
			if ((!(val2 is MonoBehaviour) || (val2 as MonoBehaviour).enabled) && (val == null || val2.GetPlacementPriority() > val.GetPlacementPriority()))
			{
				val = val2;
			}
		}
		return val;
	}
}
