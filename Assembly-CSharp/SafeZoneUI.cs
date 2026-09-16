using UnityEngine;

public class SafeZoneUI : MonoBehaviour
{
	private RectTransform m_uiRect;

	private void Start()
	{
		m_uiRect = base.gameObject.RequireComponent<RectTransform>();
		SetSafeAreaUIAnchors();
	}

	private void SetSafeAreaUIAnchors()
	{
		float num = (1f - SafeAreaAdjuster.SafeAreaWidth) * 0.5f;
		float num2 = (1f - SafeAreaAdjuster.SafeAreaHeight) * 0.5f;
		m_uiRect.anchorMin = new Vector2(num, num2);
		m_uiRect.anchorMax = new Vector2(1f - num, 1f - num2);
	}
}
