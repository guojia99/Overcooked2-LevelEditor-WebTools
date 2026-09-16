using UnityEngine;

public class FPSCounter : DebugDisplay
{
	private FPS_No_String_Allocs m_FPSCounter;

	public override void OnSetUp()
	{
		m_FPSCounter = new FPS_No_String_Allocs();
	}

	public override void OnUpdate()
	{
		m_FPSCounter.Update();
	}

	public override void OnDraw(ref Rect rect, GUIStyle style)
	{
		DrawText(ref rect, style, m_FPSCounter.GetString());
	}
}
