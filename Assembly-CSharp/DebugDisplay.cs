using UnityEngine;

public abstract class DebugDisplay
{
	public abstract void OnSetUp();

	public virtual void OnDestroy()
	{
	}

	public abstract void OnUpdate();

	public abstract void OnDraw(ref Rect rect, GUIStyle style);

	public void DrawText(ref Rect rect, GUIStyle style, string text)
	{
		GUI.Label(rect, text, style);
		rect.y += rect.height;
	}
}
