using System.Collections;

public static class CoroutineUtils
{
	public static IEnumerator ParallelRoutine(IEnumerator[] routines)
	{
		while (true)
		{
			for (int i = 0; i < routines.Length; i++)
			{
				if (routines[i] != null && !routines[i].MoveNext())
				{
					routines[i] = null;
				}
			}
			if (routines.Contains((IEnumerator x) => x != null))
			{
				yield return true;
				continue;
			}
			break;
		}
	}

	public static IEnumerator TimerRoutine(float _seconds, int _layer)
	{
		for (float t = 0f; t < _seconds; t += TimeManager.GetDeltaTime(_layer))
		{
			yield return null;
		}
	}
}
