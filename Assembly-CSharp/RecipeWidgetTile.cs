using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RecipeWidgetTile : UISubElementContainer
{
	[Serializable]
	public class TileDefinition
	{
		public List<Sprite> m_mainPictures = new List<Sprite>();

		public List<Sprite> m_modifierPictures = new List<Sprite>();

		[HideInInspector]
		public bool m_largeIcon;

		[HideInInspector]
		public bool m_backgroundTop = true;
	}

	public enum Axis
	{
		X = 0,
		Y = 1
	}

	[Serializable]
	public class DisplayConfiguration
	{
		public Sprite m_background;

		public Color m_tint = Color.white;

		public float m_iconInsetBorder = 5f;

		public float m_levelInset = 10f;

		public float m_tileSpacing = 1f;

		public float m_yExtension = 5f;

		public Vector2 m_BackgroundStitchSize = new Vector2(0f, 53f);

		[Header("Fixed")]
		[Mask(typeof(Axis))]
		public int m_fixedSizeAxis;

		public Vector2Int m_fixedSizeGridCells = Vector2Int.zero;

		public Vector2 m_fixedSizeOffset = Vector2.zero;

		[Mask(typeof(Axis))]
		public int m_fixedPosAxis;

		public Vector2Int m_fixedPosGridCells = Vector2Int.zero;

		public Vector2 m_fixedPosOffset = Vector2.zero;
	}

	[SerializeField]
	[HideInInspector]
	protected Image m_background;

	[SerializeField]
	[HideInInspector]
	protected Image m_backgroundTop;

	[SerializeField]
	[HideInInspector]
	protected Image[] m_mainImages;

	[SerializeField]
	[HideInInspector]
	protected Image[] m_modifierImages;

	protected TileDefinition m_tileDefinition;

	protected DisplayConfiguration m_displayConfig;

	private RecipeWidgetUIController.Size m_gridSizeX;

	private RecipeWidgetUIController.Size m_gridSizeY;

	public void SetupTile(TileDefinition _tileDefinition, DisplayConfiguration _displayConfig, RecipeWidgetUIController.Size _sizex, RecipeWidgetUIController.Size _sizey)
	{
		m_tileDefinition = _tileDefinition;
		m_displayConfig = _displayConfig;
		m_gridSizeX = _sizex;
		m_gridSizeY = _sizey;
	}

	protected override void OnCreateSubObjects(GameObject _container)
	{
		m_background = CreateImage(_container, "RecipeWidgetTile_Background");
		m_background.type = Image.Type.Sliced;
		if (m_tileDefinition.m_backgroundTop)
		{
			m_backgroundTop = CreateImage(m_background.gameObject, "RecipeWidgetTile_BackgroundTop");
			m_backgroundTop.type = Image.Type.Sliced;
		}
		RectTransform rectTransform = CreateRect(_container, "RecipeWidgetTile_GridContainer");
		UIUtils.SetupFillParentAreaRect(rectTransform);
		rectTransform.offsetMin = new Vector2(m_gridSizeX.PixelOffset * 0.5f, m_gridSizeY.PixelOffset * 0.5f);
		rectTransform.offsetMax = new Vector2((0f - m_gridSizeX.PixelOffset) * 0.5f, 0f - m_gridSizeY.PixelOffset * 0.5f);
		m_mainImages = new Image[m_tileDefinition.m_mainPictures.Count];
		for (int i = 0; i < m_mainImages.Length; i++)
		{
			m_mainImages[i] = CreateImage(rectTransform.gameObject, "RecipeWidgetTile_MainImage_" + i);
		}
		if (m_tileDefinition.m_modifierPictures != null && m_tileDefinition.m_modifierPictures.Count > 0)
		{
			m_modifierImages = new Image[m_tileDefinition.m_modifierPictures.Count];
			for (int j = 0; j < m_modifierImages.Length; j++)
			{
				m_modifierImages[j] = CreateImage(rectTransform.gameObject, "RecipeWidgetTile_ModifierImage_" + j);
			}
		}
	}

	protected override void OnRefreshSubObjectProperties(GameObject _container)
	{
		m_background.sprite = m_displayConfig.m_background;
		m_background.color = m_displayConfig.m_tint;
		for (int i = 0; i < m_mainImages.Length; i++)
		{
			m_mainImages[i].sprite = m_tileDefinition.m_mainPictures[i];
		}
		if (m_modifierImages != null)
		{
			for (int j = 0; j < m_modifierImages.Length; j++)
			{
				m_modifierImages[j].sprite = m_tileDefinition.m_modifierPictures[j];
			}
		}
		UIUtils.SetupFillParentAreaRect(m_background.gameObject.RequireComponent<RectTransform>());
		if (m_backgroundTop != null)
		{
			m_backgroundTop.sprite = m_displayConfig.m_background;
			m_backgroundTop.color = m_displayConfig.m_tint;
			RectTransform rectTransform = m_backgroundTop.gameObject.RequireComponent<RectTransform>();
			UIUtils.SetupFillParentAreaRect(rectTransform);
			rectTransform.anchorMin = new Vector2(0f, 1f);
			rectTransform.anchorMax = new Vector2(1f, 1f);
			rectTransform.pivot = new Vector2(0.5f, 1f);
			rectTransform.localScale = new Vector3(1f, -1f, 1f);
			rectTransform.anchoredPosition = new Vector3(0f, 0f, 0f);
			rectTransform.sizeDelta = m_displayConfig.m_BackgroundStitchSize;
			rectTransform.offsetMax = new Vector2(0f, rectTransform.offsetMax.y);
			rectTransform.offsetMin = new Vector2(0f, rectTransform.offsetMin.y);
		}
		int num = ((!m_tileDefinition.m_largeIcon) ? 1 : 2);
		for (int k = 0; k < m_mainImages.Length; k++)
		{
			float x = (float)k + (float)(m_gridSizeX.GridTiles - m_mainImages.Length) * 0.5f - 0.5f * (float)(num - 1);
			SetRectForGridPos(m_mainImages[k].gameObject.RequireComponent<RectTransform>(), x, m_gridSizeY.GridTiles - num, num, num);
		}
		if (m_modifierImages != null)
		{
			for (int l = 0; l < m_modifierImages.Length; l++)
			{
				float x2 = (float)l + (float)(m_gridSizeX.GridTiles - m_modifierImages.Length) * 0.5f;
				SetRectForGridPos(m_modifierImages[l].gameObject.RequireComponent<RectTransform>(), x2, 0f, 1f, 1f);
			}
		}
	}

	private void SetRectForGridPos(RectTransform _rect, float _x, float _y, float _sizeX, float _sizeY)
	{
		Vector2 vector = new Vector2(1f / (float)m_gridSizeX.GridTiles, 1f / (float)m_gridSizeY.GridTiles);
		_x = Mathf.Clamp(_x, 0f, (float)m_gridSizeX.GridTiles - 1f);
		_y = Mathf.Clamp(_y, 0f, (float)m_gridSizeY.GridTiles - 1f);
		_rect.anchorMin = new Vector2(vector.x * _x, vector.y * _y);
		_rect.anchorMax = _rect.anchorMin + new Vector2(_sizeX * vector.x, _sizeY * vector.y);
		_rect.pivot = new Vector2(0f, 1f);
		float iconInsetBorder = m_displayConfig.m_iconInsetBorder;
		_rect.offsetMin = new Vector2(iconInsetBorder, iconInsetBorder);
		_rect.offsetMax = new Vector2(0f - iconInsetBorder, 0f - iconInsetBorder);
		_rect.localScale = Vector3.one;
	}
}
