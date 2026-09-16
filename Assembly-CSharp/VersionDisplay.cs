using UnityEngine;

public class VersionDisplay : DebugDisplay
{
	private string m_Text = string.Empty;

	public override void OnSetUp()
	{
		m_Text = "Build #" + BuildVersion.m_VersionString;
	}

	public override void OnUpdate()
	{
	}

	public override void OnDraw(ref Rect rect, GUIStyle style)
	{
		DrawText(ref rect, style, m_Text);
	}
}
