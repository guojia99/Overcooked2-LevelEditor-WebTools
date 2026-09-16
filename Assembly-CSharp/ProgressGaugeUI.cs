using System;
using UnityEngine;
using UnityEngine.UI;

[ExecuteInEditMode]
public class ProgressGaugeUI : BaseProgressBarUI
{
	[SerializeField]
	private float m_emptyAngle = 90f;

	[SerializeField]
	private float m_fullAngle = -90f;

	protected override void UpdateFill()
	{
		Image image = m_images[1];
		if (image != null)
		{
			RectTransform rectTransform = image.rectTransform;
			rectTransform.localRotation = ProgressToRotation(base.Value);
		}
	}

	private Quaternion ProgressToRotation(float _progress)
	{
		float z = MathUtils.Remap(_progress, 0f, 1f, m_emptyAngle, m_fullAngle);
		return Quaternion.Euler(0f, 0f, z);
	}

	protected override Image CreateFillImage(GameObject _rect)
	{
		RectTransform component = _rect.GetComponent<RectTransform>();
		component.pivot = new Vector2(0.5f, 0f);
		component.sizeDelta = new Vector2(0f, 0f);
		component.anchorMin = new Vector2(0.4f, 0.15f);
		component.anchorMax = new Vector2(0.6f, 0.85f);
		return _rect.GetComponent<Image>();
	}

	protected override void PositionNotch(Image _notch, float _position)
	{
		RectTransform rectTransform = _notch.rectTransform;
		rectTransform.pivot = new Vector2(0.5f, 0f);
		rectTransform.sizeDelta = new Vector2(5f, 0f);
		rectTransform.anchorMin = new Vector2(_position, Mathf.Sin(_position * (float)Math.PI));
		rectTransform.anchorMax = new Vector2(_position, Mathf.Sin(_position * (float)Math.PI));
		rectTransform.localRotation = ProgressToRotation(_position);
	}
}
