using System;
using UnityEngine;

public class TopRecipeWidgetTile : RecipeWidgetTile
{
	[Serializable]
	public class TopDisplayConfiguration : DisplayConfiguration
	{
		[Header("ProgressBar")]
		public bool m_hasProgressBar = true;

		public Vector2 m_progressBarAnchorMin = Vector2.zero;

		public Vector2 m_progressBarAnchorMax = Vector2.one;

		public Vector2 m_progressBarOffsetMin = Vector2.zero;

		public Vector2 m_progressBarOffsetMax = Vector2.zero;

		public BaseProgressBarUI.ColourChangingImageConfig m_progressUIBackground;

		public BaseProgressBarUI.ColourChangingImageConfig m_progressUIFill;

		public Sprite lowTipSprite;

		public Sprite highTipSprite;

		public Color notchColor;

		public float[] m_NotchOffsets;
	}

	[SerializeField]
	private ProgressBarUI m_progressBar;

	private float m_value = 1f;

	public void SetProgress(float _value)
	{
		m_value = _value;
		if (m_progressBar != null)
		{
			m_progressBar.SetValue(m_value);
		}
	}

	public float GetProgress()
	{
		return m_value;
	}

	protected override void OnCreateSubObjects(GameObject _container)
	{
		base.OnCreateSubObjects(_container);
		TopDisplayConfiguration topDisplayConfiguration = m_displayConfig as TopDisplayConfiguration;
		if (topDisplayConfiguration.m_hasProgressBar)
		{
			RectTransform rectTransform = CreateRect(_container, "ProgressBar");
			UIUtils.SetupFillParentAreaRect(rectTransform);
			rectTransform.pivot = new Vector2(0f, 1f);
			rectTransform.SetSiblingIndex(1);
			rectTransform.anchorMax = topDisplayConfiguration.m_progressBarAnchorMax;
			rectTransform.anchorMin = topDisplayConfiguration.m_progressBarAnchorMin;
			rectTransform.offsetMin = topDisplayConfiguration.m_progressBarOffsetMin;
			rectTransform.offsetMax = topDisplayConfiguration.m_progressBarOffsetMax;
			m_progressBar = rectTransform.gameObject.AddComponent<ProgressBarUI>();
			m_progressBar.SetSprites(topDisplayConfiguration.m_progressUIBackground, topDisplayConfiguration.m_progressUIFill);
			m_progressBar.SetValue(m_value);
			m_progressBar.SetNotchs(new Sprite[2] { topDisplayConfiguration.lowTipSprite, topDisplayConfiguration.highTipSprite }, topDisplayConfiguration.m_NotchOffsets, topDisplayConfiguration.notchColor);
			if (Application.isPlaying)
			{
				m_progressBar.RefreshSubElements();
			}
		}
	}
}
