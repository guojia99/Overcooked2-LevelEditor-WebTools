using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneNameDisplay : DebugDisplay
{
	public string m_displayString = string.Empty;

	public override void OnSetUp()
	{
		m_displayString = "Scene: " + SceneManager.GetActiveScene().name;
	}

	public override void OnUpdate()
	{
	}

	public override void OnDraw(ref Rect rect, GUIStyle style)
	{
		DrawText(ref rect, style, m_displayString);
	}
}
