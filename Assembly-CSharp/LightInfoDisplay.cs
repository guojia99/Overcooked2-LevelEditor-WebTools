using UnityEngine;

public class LightInfoDisplay : DebugDisplay
{
	public bool m_bSceneHasLightmaps;

	public string m_displayString = string.Empty;

	public override void OnSetUp()
	{
		m_bSceneHasLightmaps = LightmapSettings.lightmaps != null && LightmapSettings.lightmaps.Length > 0;
		RefreshString();
	}

	public override void OnUpdate()
	{
	}

	public override void OnDraw(ref Rect rect, GUIStyle style)
	{
		DrawText(ref rect, style, m_displayString);
	}

	private void RefreshString()
	{
		m_displayString = "Lightmaps: " + ((!m_bSceneHasLightmaps) ? "No" : "Yes");
	}
}
