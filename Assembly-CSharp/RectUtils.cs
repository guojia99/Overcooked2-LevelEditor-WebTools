using UnityEngine;

public static class RectUtils
{
	public static Rect Added(this Rect original, float x, float y)
	{
		return new Rect(original.x + x, original.y + y, original.width, original.height);
	}

	public static Rect Added(this Rect original, Vector2 offset)
	{
		return new Rect(original.x + offset.x, original.y + offset.y, original.width, original.height);
	}

	public static Rect SizeDivided(this Rect original, float x, float y)
	{
		return new Rect(original.x, original.y, original.width / x, original.height / y);
	}

	public static Rect SizeMultiplied(this Rect original, float x, float y)
	{
		return new Rect(original.x, original.y, original.width * x, original.height * y);
	}

	public static Rect FullMultiplied(this Rect original, float x, float y)
	{
		return new Rect(original.x * x, original.y * y, original.width * x, original.height * y);
	}

	public static Rect ResizesAboutCentre(this Rect original, float width, float height)
	{
		float x = original.x + 0.5f * (original.width - width);
		float y = original.y + 0.5f * (original.height - height);
		return new Rect(x, y, width, height);
	}

	public static Rect WithCentre(this Rect original, float x, float y)
	{
		return new Rect(x - 0.5f * original.width, y - 0.5f * original.height, original.width, original.height);
	}

	public static Rect ToWorld(this Rect _parent, Rect _local)
	{
		float x = MathUtils.Remap(_local.x, 0f, 1f, _parent.x, _parent.x + _parent.width);
		float y = MathUtils.Remap(_local.y, 0f, 1f, _parent.y, _parent.y + _parent.height);
		float width = _local.width * _parent.width;
		float height = _local.height * _parent.height;
		return new Rect(x, y, width, height);
	}

	public static Rect ToLocal(this Rect _parent, Rect _world)
	{
		float x = MathUtils.Remap(_world.x, _parent.x, _parent.x + _parent.width, 0f, 1f);
		float y = MathUtils.Remap(_world.y, _parent.y, _parent.y + _parent.height, 0f, 1f);
		float width = _world.width / _parent.width;
		float height = _world.height / _parent.height;
		return new Rect(x, y, width, height);
	}
}
