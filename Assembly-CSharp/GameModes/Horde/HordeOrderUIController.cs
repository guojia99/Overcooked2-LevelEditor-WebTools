using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GameModes.Horde
{
	[ExecuteInEditMode]
	[RequireComponent(typeof(RectTransform))]
	public class HordeOrderUIController : HoverIconUIController
	{
		public enum Align
		{
			Left = 0,
			Right = 1
		}

		[SerializeField]
		private RecipeWidgetTile.DisplayConfiguration m_displayConfig = new RecipeWidgetTile.DisplayConfiguration();

		[SerializeField]
		private TopRecipeWidgetTile.TopDisplayConfiguration m_topDisplayConfig = new TopRecipeWidgetTile.TopDisplayConfiguration();

		[SerializeField]
		private Animator m_animator;

		[SerializeField]
		private AnimationCurve m_appearScaleCurve = AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1f);

		private WidgetAnimation m_animation;

		private Image[] m_images;

		private RectTransformExtension m_rectTransformExtension;

		private Vector2 m_offset;

		private Vector2 m_size;

		public void Setup(Transform followTransform, RecipeWidgetUIController.RecipeTileData[] guiDescriptions, Align uiAnchor)
		{
			SetFollowTransform(followTransform);
			m_rectTransform = base.gameObject.RequireComponent<RectTransform>();
			m_rectTransformExtension = base.gameObject.RequireComponent<RectTransformExtension>();
			OnCreateSubObjects(base.gameObject, guiDescriptions, m_displayConfig, uiAnchor);
			m_rectTransformExtension.PixelOffset += m_offset;
			m_images = base.gameObject.RequestComponentsRecursive<Image>();
		}

		public void PlayAnimation(WidgetAnimation animation)
		{
			m_animation = animation;
			m_animation.Init(m_animator);
		}

		public void Update()
		{
			if (m_animation == null)
			{
				return;
			}
			Color color = Color.white;
			Vector2 scale = Vector2.one;
			Vector2 vector = Vector2.zero;
			float deltaTime = TimeManager.GetDeltaTime(base.gameObject);
			m_animation.Advance(deltaTime);
			if (!m_animation.IsFinished())
			{
				color = m_animation.GetColourModifier();
				scale = m_animation.GetScaleModifier();
				vector = ((!(deltaTime > 0f)) ? Vector2.zero : m_animation.GetPosModifier());
			}
			else
			{
				m_animation = null;
			}
			m_rectTransformExtension.PixelOffset += vector;
			m_rectTransformExtension.scale = scale;
			if (m_images == null || m_images.Length <= 0)
			{
				return;
			}
			for (int i = 0; i < m_images.Length; i++)
			{
				Image image = m_images[i];
				if (image != null)
				{
					Color color2 = Color.white;
					if (image.sprite == m_topDisplayConfig.m_background)
					{
						color2 = m_topDisplayConfig.m_tint;
					}
					else if (image.sprite == m_displayConfig.m_background)
					{
						color2 = m_displayConfig.m_tint;
					}
					else if (image.sprite == m_topDisplayConfig.lowTipSprite || image.sprite == m_topDisplayConfig.highTipSprite)
					{
						color2 = m_topDisplayConfig.notchColor;
					}
					image.color = color2 * color;
				}
			}
		}

		public bool IsPlayingAnimation()
		{
			return m_animation != null;
		}

		private RecipeWidgetUIController.Size?[] BuildTileSizes(RecipeWidgetUIController.RecipeTileData[] tileDatas, RecipeWidgetTile.DisplayConfiguration displayConfig)
		{
			RecipeWidgetUIController.Size?[] array = new RecipeWidgetUIController.Size?[tileDatas.Length];
			for (int i = 0; i < tileDatas.Length; i++)
			{
				CalculateSize(tileDatas, i, array, displayConfig);
			}
			return array;
		}

		private RecipeWidgetUIController.Position?[] BuildTilePositions(RecipeWidgetUIController.RecipeTileData[] tileDatas, RecipeWidgetUIController.Size?[] _sizes, RecipeWidgetTile.DisplayConfiguration displayConfig)
		{
			RecipeWidgetUIController.Position?[] array = new RecipeWidgetUIController.Position?[tileDatas.Length];
			for (int i = 0; i < tileDatas.Length; i++)
			{
				CalculatePosition(tileDatas, i, array, _sizes, displayConfig);
			}
			return array;
		}

		private RecipeWidgetUIController.Size CalculateSize(RecipeWidgetUIController.RecipeTileData[] tileDatas, int _tileIdx, RecipeWidgetUIController.Size?[] _cachedSizes, RecipeWidgetTile.DisplayConfiguration displayConfig)
		{
			RecipeWidgetUIController.RecipeTileData recipeTileData = tileDatas[_tileIdx];
			if (_cachedSizes[_tileIdx].HasValue)
			{
				return _cachedSizes[_tileIdx].Value;
			}
			RecipeWidgetUIController.Size size;
			if (recipeTileData.m_children.Count > 0)
			{
				size = new RecipeWidgetUIController.Size(0, 2f * displayConfig.m_levelInset + displayConfig.m_tileSpacing * (float)(recipeTileData.m_children.Count - 1));
				for (int i = 0; i < recipeTileData.m_children.Count; i++)
				{
					RecipeWidgetUIController.Size size2 = CalculateSize(tileDatas, recipeTileData.m_children[i], _cachedSizes, displayConfig);
					size.GridTiles += size2.GridTiles;
					size.PixelOffset += size2.PixelOffset;
				}
				size.GridTiles = Mathf.Max(size.GridTiles, 2);
			}
			else
			{
				int num = recipeTileData.m_tileDefinition.m_mainPictures.Count;
				if (recipeTileData.m_tileDefinition.m_modifierPictures != null)
				{
					num = Mathf.Max(num, recipeTileData.m_tileDefinition.m_modifierPictures.Count);
				}
				size = new RecipeWidgetUIController.Size(num, 2f * displayConfig.m_levelInset);
			}
			_cachedSizes[_tileIdx] = size;
			return size;
		}

		private RecipeWidgetUIController.Position CalculatePosition(RecipeWidgetUIController.RecipeTileData[] tileDatas, int _tileIdx, RecipeWidgetUIController.Position?[] _cachedPositions, RecipeWidgetUIController.Size?[] _sizes, RecipeWidgetTile.DisplayConfiguration displayConfig)
		{
			RecipeWidgetUIController.RecipeTileData recipeTileData = tileDatas[_tileIdx];
			if (_cachedPositions[_tileIdx].HasValue)
			{
				return _cachedPositions[_tileIdx].Value;
			}
			int parentIndex = GetParentIndex(tileDatas, _tileIdx);
			RecipeWidgetUIController.Position position;
			if (parentIndex != -1)
			{
				position = CalculatePosition(tileDatas, parentIndex, _cachedPositions, _sizes, displayConfig);
				position.X.PixelOffset += displayConfig.m_levelInset;
				position.Y.GridTiles = position.Y.GridTiles + CalculateSizeY(tileDatas, parentIndex, displayConfig).GridTiles;
				RecipeWidgetUIController.RecipeTileData recipeTileData2 = tileDatas[parentIndex];
				for (int i = 0; i < recipeTileData2.m_children.Count; i++)
				{
					int num = recipeTileData2.m_children[i];
					if (num == _tileIdx)
					{
						break;
					}
					position.X.GridTiles += _sizes[num].Value.GridTiles;
					position.X.PixelOffset += _sizes[num].Value.PixelOffset + displayConfig.m_tileSpacing;
				}
			}
			else
			{
				position = new RecipeWidgetUIController.Position(new RecipeWidgetUIController.Size(0, 0f), new RecipeWidgetUIController.Size(-2, 0f));
			}
			_cachedPositions[_tileIdx] = position;
			return position;
		}

		private int GetParentIndex(RecipeWidgetUIController.RecipeTileData[] tileDatas, int _childIdx)
		{
			for (int i = 0; i < tileDatas.Length; i++)
			{
				RecipeWidgetUIController.RecipeTileData recipeTileData = tileDatas[i];
				for (int j = 0; j < recipeTileData.m_children.Count; j++)
				{
					if (recipeTileData.m_children[j] == _childIdx)
					{
						return i;
					}
				}
			}
			return -1;
		}

		private RecipeWidgetUIController.Size CalculateSizeY(RecipeWidgetUIController.RecipeTileData[] tileDatas, int _tileIdx, RecipeWidgetTile.DisplayConfiguration displayConfig)
		{
			RecipeWidgetUIController.RecipeTileData recipeTileData = tileDatas[_tileIdx];
			bool flag = recipeTileData.m_children.Count > 0;
			bool flag2 = recipeTileData.m_tileDefinition.m_modifierPictures != null && recipeTileData.m_tileDefinition.m_modifierPictures.Count > 0;
			int num = 0;
			num = ((!MaskUtils.HasFlag(displayConfig.m_fixedSizeAxis, RecipeWidgetTile.Axis.Y)) ? ((!flag && !flag2) ? 1 : 2) : displayConfig.m_fixedSizeGridCells.y);
			return new RecipeWidgetUIController.Size(num, displayConfig.m_yExtension);
		}

		private RecipeWidgetUIController.RecipeTileData FindHead(RecipeWidgetUIController.RecipeTileData[] _tileData)
		{
			bool[] array = new bool[_tileData.Length];
			for (int i = 0; i < _tileData.Length; i++)
			{
				for (int j = 0; j < _tileData[i].m_children.Count; j++)
				{
					array[_tileData[i].m_children[j]] = true;
				}
			}
			for (int k = 0; k < array.Length; k++)
			{
				if (!array[k])
				{
					return _tileData[k];
				}
			}
			return null;
		}

		private void CalculateRect(RectTransform rect, out RecipeWidgetUIController.Size tileSizeX, out RecipeWidgetUIController.Size tileSizeY, out RecipeWidgetUIController.Position tilePos, RecipeWidgetUIController.RecipeTileData[] tileDatas, RecipeWidgetUIController.Size?[] tileSizes, RecipeWidgetUIController.Position?[] tilePositions, int index, RecipeWidgetTile.DisplayConfiguration displayConfig)
		{
			tileSizeX = tileSizes[index].Value;
			tileSizeY = CalculateSizeY(tileDatas, index, displayConfig);
			tilePos = tilePositions[index].Value;
			if (MaskUtils.HasFlag(displayConfig.m_fixedSizeAxis, RecipeWidgetTile.Axis.X))
			{
				tileSizeX = new RecipeWidgetUIController.Size(displayConfig.m_fixedSizeGridCells.x, displayConfig.m_fixedSizeOffset.x);
			}
			if (MaskUtils.HasFlag(displayConfig.m_fixedSizeAxis, RecipeWidgetTile.Axis.Y))
			{
				tileSizeY = new RecipeWidgetUIController.Size(displayConfig.m_fixedSizeGridCells.y, displayConfig.m_fixedSizeOffset.y);
			}
			if (MaskUtils.HasFlag(displayConfig.m_fixedPosAxis, RecipeWidgetTile.Axis.X))
			{
				tilePos.X = new RecipeWidgetUIController.Size(displayConfig.m_fixedPosGridCells.x, displayConfig.m_fixedPosOffset.x);
			}
			if (MaskUtils.HasFlag(displayConfig.m_fixedPosAxis, RecipeWidgetTile.Axis.Y))
			{
				tilePos.Y = new RecipeWidgetUIController.Size(displayConfig.m_fixedPosGridCells.y, displayConfig.m_fixedPosOffset.y);
			}
			rect.anchorMin = new Vector2((float)tilePos.X.GridTiles * 0.5f, (float)(-(tilePos.Y.GridTiles + tileSizeY.GridTiles)) * 0.5f);
			rect.anchorMax = new Vector2((float)(tilePos.X.GridTiles + tileSizeX.GridTiles) * 0.5f, (float)(-tilePos.Y.GridTiles) * 0.5f);
			rect.offsetMin = new Vector2(tilePos.X.PixelOffset, tilePos.Y.PixelOffset);
			rect.offsetMax = new Vector2(tilePos.X.PixelOffset + tileSizeX.PixelOffset, tilePos.Y.PixelOffset + tileSizeY.PixelOffset);
			rect.localScale = Vector3.one;
		}

		private void OnCreateSubObjects(GameObject _container, RecipeWidgetUIController.RecipeTileData[] tileDatas, RecipeWidgetTile.DisplayConfiguration displayConfig, Align align)
		{
			List<RecipeWidgetTile> list = new List<RecipeWidgetTile>();
			RecipeWidgetUIController.Size?[] array = BuildTileSizes(tileDatas, displayConfig);
			RecipeWidgetUIController.Position?[] tilePositions = BuildTilePositions(tileDatas, array, displayConfig);
			RecipeWidgetUIController.RecipeTileData recipeTileData = FindHead(tileDatas);
			for (int i = 0; i < tileDatas.Length; i++)
			{
				RecipeWidgetUIController.RecipeTileData recipeTileData2 = tileDatas[i];
				GameObject gameObject = GameObjectUtils.CreateOnParent<RectTransform>(_container, "Tile " + i);
				RectTransform component = gameObject.GetComponent<RectTransform>();
				recipeTileData2.m_tileDefinition.m_largeIcon = recipeTileData2.m_children.Count > 0;
				recipeTileData2.m_tileDefinition.m_backgroundTop = false;
				RecipeWidgetUIController.Size tileSizeX;
				RecipeWidgetUIController.Size tileSizeY;
				RecipeWidgetUIController.Position tilePos;
				RecipeWidgetTile recipeWidgetTile;
				if (recipeTileData2 == recipeTileData)
				{
					CalculateRect(component, out tileSizeX, out tileSizeY, out tilePos, tileDatas, array, tilePositions, i, m_topDisplayConfig);
					recipeWidgetTile = gameObject.AddComponent<TopRecipeWidgetTile>();
					recipeWidgetTile.SetupTile(recipeTileData2.m_tileDefinition, m_topDisplayConfig, tileSizeX, tileSizeY);
					TopRecipeWidgetTile topRecipeWidgetTile = recipeWidgetTile as TopRecipeWidgetTile;
				}
				else
				{
					CalculateRect(component, out tileSizeX, out tileSizeY, out tilePos, tileDatas, array, tilePositions, i, displayConfig);
					recipeWidgetTile = gameObject.AddComponent<RecipeWidgetTile>();
					recipeWidgetTile.SetupTile(recipeTileData2.m_tileDefinition, displayConfig, tileSizeX, tileSizeY);
				}
				list.Add(recipeWidgetTile);
			}
			list.Reverse();
			for (int j = 0; j < list.Count; j++)
			{
				list[j].transform.SetSiblingIndex(j);
			}
			if (!Application.isPlaying)
			{
				return;
			}
			for (int k = 0; k < list.Count; k++)
			{
				list[k].RefreshSubElements();
			}
			float num = 0f;
			float num2 = float.NegativeInfinity;
			int childCount = base.transform.childCount;
			for (int l = 0; l < list.Count; l++)
			{
				RectTransform rectTransform = base.transform.GetChild(l).transform as RectTransform;
				if (rectTransform != null)
				{
					num += rectTransform.rect.width;
					num2 = Mathf.Max(rectTransform.rect.height, num2);
				}
			}
			m_size = new Vector2(num, num2);
			switch (align)
			{
			case Align.Left:
				m_offset = new Vector2(0f, 0f).MultipliedBy(m_size);
				break;
			case Align.Right:
				m_offset = new Vector2(-1f, 0f).MultipliedBy(m_size);
				break;
			}
		}
	}
}
