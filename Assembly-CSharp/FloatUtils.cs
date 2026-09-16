using UnityEngine;

public static class FloatUtils
{
	public static int ToUnorm(float f, int bitCount)
	{
		f = Mathf.Clamp01(f);
		return (int)(f * ((float)(1 << bitCount - 1 - 1) + 0.5f));
	}

	public static float FromUnorm(int i, int bitCount)
	{
		return Mathf.Clamp01((float)i / (float)(1 << bitCount - 1 - 1));
	}
}
