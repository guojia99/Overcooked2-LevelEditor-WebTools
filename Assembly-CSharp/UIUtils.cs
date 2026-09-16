using UnityEngine;

public static class UIUtils
{
	public static void SetupFillParentAreaRect(RectTransform _rect)
	{
		_rect.anchorMin = new Vector2(0f, 0f);
		_rect.anchorMax = new Vector2(1f, 1f);
		_rect.offsetMin = Vector2.zero;
		_rect.offsetMax = Vector2.zero;
		_rect.localScale = Vector3.one;
	}
}
