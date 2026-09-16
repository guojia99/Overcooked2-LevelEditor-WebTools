using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[ExecuteInEditMode]
public class ProgressBarUI : BaseProgressBarUI
{
	[SerializeField]
	[Range(0f, 1f)]
	private float m_scalingFactor = 1f;

	private Image m_filledImage;

	private Image m_capImage;

	private bool? m_hasMask;

	private List<RectTransform> m_allChildRects = new List<RectTransform>(0);

	protected override void UpdateFill()
	{
		if (m_filledImage == null)
		{
			m_filledImage = base.FillImage;
		}
		if ((bool)m_filledImage)
		{
			float num = (1f - m_scalingFactor) * 0.5f;
			float num2 = base.Value * m_scalingFactor + num;
			if (!m_hasMask.HasValue && m_filledImage.transform.parent != null)
			{
				m_hasMask = m_filledImage.transform.parent.gameObject.RequestComponent<Mask>() != null;
			}
			bool flag = false;
			if (m_filledImage.transform.parent != null && m_hasMask.HasValue && m_hasMask.Value)
			{
				RectTransform rectTransform = m_filledImage.transform.parent.transform as RectTransform;
				flag = rectTransform != null;
				if (rectTransform != null)
				{
					Vector2 anchorMax = rectTransform.anchorMax;
					anchorMax.x = base.Value;
					rectTransform.anchorMax = anchorMax;
					flag = true;
				}
			}
			if (flag)
			{
				RectTransform rectTransform2 = m_filledImage.transform.parent.transform as RectTransform;
				rectTransform2.anchorMax = new Vector2(base.Value, rectTransform2.anchorMax.y);
			}
			else
			{
				RectTransform component = m_filledImage.gameObject.GetComponent<RectTransform>();
				component.localScale = new Vector2(base.Value, 1f);
			}
		}
		if (m_capImage == null)
		{
			m_capImage = base.CapImage;
		}
		if (m_capImage != null)
		{
			RectTransform rectTransform3 = null;
			rectTransform3 = ((!(m_filledImage.transform.parent != null) || !m_hasMask.HasValue || !m_hasMask.Value) ? m_filledImage.gameObject.GetComponent<RectTransform>() : (m_filledImage.transform.parent.transform as RectTransform));
			RectTransform component2 = m_capImage.gameObject.GetComponent<RectTransform>();
			component2.anchorMin = new Vector2(rectTransform3.anchorMax.x, component2.anchorMin.y);
			component2.anchorMax = new Vector2(rectTransform3.anchorMax.x, component2.anchorMax.y);
		}
	}

	protected override void OnCreateSubObjects(GameObject _container)
	{
		base.OnCreateSubObjects(_container);
		m_allChildRects.Clear();
		RectTransform[] array = _container.RequestComponentsRecursive<RectTransform>();
		foreach (RectTransform rectTransform in array)
		{
			bool flag = false;
			for (int j = 0; j < m_notches.Length; j++)
			{
				if (m_notches[j].rectTransform == rectTransform)
				{
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				m_allChildRects.Add(rectTransform);
			}
		}
	}

	protected override void OnRefreshSubObjectProperties(GameObject _container)
	{
		for (int i = 0; i < m_allChildRects.Count; i++)
		{
			UIUtils.SetupFillParentAreaRect(m_allChildRects[i]);
			m_allChildRects[i].pivot = new Vector2(0f, 1f);
		}
		base.OnRefreshSubObjectProperties(_container);
	}

	protected override Image CreateFillImage(GameObject _rect)
	{
		GameObject gameObject = GameObjectUtils.CreateOnParent<Image>(_rect.gameObject, "SubImage");
		gameObject.hideFlags = HideFlags.NotEditable;
		return gameObject.GetComponent<Image>();
	}

	protected override void PositionNotch(Image _notch, float _position)
	{
		RectTransform rectTransform = _notch.rectTransform;
		rectTransform.pivot = new Vector2(0.5f, 0f);
		rectTransform.sizeDelta = new Vector2(5f, 0f);
		rectTransform.anchorMin = new Vector2(_position, 0f);
		rectTransform.anchorMax = new Vector2(_position, 1f);
	}
}
