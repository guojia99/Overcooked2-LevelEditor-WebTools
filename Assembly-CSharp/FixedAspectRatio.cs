using UnityEngine;

public static class FixedAspectRatio
{
	public static readonly Vector2 DefaultAspect = new Vector2(16f, 9f);

	public static Rect ComputeAspectRatioRect(float aspectX, float aspectY, float width, float height)
	{
		float num = aspectX / aspectY;
		float num2 = width / height;
		float num3 = num2 / num;
		float num4 = 1f / num3;
		return (!(num3 < 1f)) ? new Rect((1f - num4) / 2f, 0f, num4, 1f) : new Rect(0f, (1f - num3) / 2f, 1f, num3);
	}
}
