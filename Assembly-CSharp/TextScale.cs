using UnityEngine;

[ExecuteInEditMode]
public class TextScale : MonoBehaviour
{
	[SerializeField]
	private Vector3 m_baseScale = new Vector3(1f, 1f, 1f);

	[SerializeField]
	private float m_baseParentSize = 0.3f;

	private RectTransform m_rectTransform;

	private void Awake()
	{
		UpdateSize();
	}

	private void UpdateSize()
	{
		RectTransform rectTransform = base.transform.parent as RectTransform;
		Vector3[] array = new Vector3[4];
		float num = (float)Camera.main.pixelWidth * (rectTransform.anchorMax - rectTransform.anchorMin).x;
		float num2 = num / m_baseParentSize;
		base.transform.localScale = m_baseScale * num2;
	}
}
