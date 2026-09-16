using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(HoverIconUIController))]
public class IngredientContentsUIContainer : UISubElementContainer
{
	[SerializeField]
	private OrderDefinitionNode m_orderDefinitionNode;

	[SerializeField]
	private Sprite m_burntSprite;

	[SerializeField]
	private Sprite m_overmixedSprite;

	[SerializeField]
	private Sprite m_emptySprite;

	private AssembledDefinitionNode m_runtimeOrderDefinition;

	private int m_minimumSize;

	private List<Image> m_icons = new List<Image>();

	private int m_iconsInUse;

	private UI_Move m_uiMove;

	private void Awake()
	{
		m_runtimeOrderDefinition = m_orderDefinitionNode.Simpilfy();
		m_uiMove = base.gameObject.RequestComponent<UI_Move>();
		TryGetExistingContainer();
		TryGetExistingImages();
	}

	private void TryGetExistingContainer()
	{
		Transform transform = base.transform;
		for (int i = 0; i < transform.childCount; i++)
		{
			Transform child = transform.GetChild(i);
			if (child.name.Equals(c_containerName))
			{
				m_container = child.gameObject;
				break;
			}
		}
	}

	private void TryGetExistingImages()
	{
		if (m_container == null)
		{
			return;
		}
		Transform transform = m_container.transform;
		int i = 0;
		for (int childCount = transform.childCount; i < childCount; i++)
		{
			GameObject obj = transform.GetChild(i).gameObject;
			Image image = obj.RequestComponent<Image>();
			if (image != null)
			{
				m_icons.Add(image);
			}
		}
	}

	public void SetOrder(AssembledDefinitionNode _orderDefinition)
	{
		if (!AssembledDefinitionNode.Matching(m_runtimeOrderDefinition, _orderDefinition))
		{
			m_runtimeOrderDefinition = _orderDefinition;
			RefreshSubElements();
		}
	}

	protected override void EnsureImagesExist()
	{
		if (!(m_container != null))
		{
			m_container = GameObjectUtils.CreateOnParent<RectTransform>(base.gameObject, c_containerName);
			AnchorGridLayoutGroup anchorGridLayoutGroup = m_container.AddComponent<AnchorGridLayoutGroup>();
			anchorGridLayoutGroup.cellSize = new Vector2(0.5f, 0.5f);
			anchorGridLayoutGroup.childAlignment = TextAnchor.MiddleCenter;
			OnCreateSubObjects(m_container);
		}
	}

	protected override void OnRefreshSubObjectProperties(GameObject _container)
	{
		RefreshIcons(_container);
	}

	private void RefreshIcons(GameObject _container)
	{
		SetIconsForOrder(_container, m_runtimeOrderDefinition);
		UpdateIconVisibility();
		if (m_uiMove != null)
		{
			m_uiMove.UpdateGraphics();
		}
	}

	protected void SetIconsForOrder(GameObject _container, AssembledDefinitionNode _order)
	{
		m_iconsInUse = 0;
		if (_order == null || _order.Simpilfy() == AssembledDefinitionNode.NullNode)
		{
			SetRemainingIcons(_container, m_emptySprite);
			return;
		}
		CookedCompositeAssembledNode cookedCompositeAssembledNode = _order as CookedCompositeAssembledNode;
		if (cookedCompositeAssembledNode != null && cookedCompositeAssembledNode.m_progress == CookedCompositeOrderNode.CookingProgress.Burnt)
		{
			SetNextIcon(_container, m_burntSprite);
			return;
		}
		MixedCompositeAssembledNode mixedCompositeAssembledNode = _order as MixedCompositeAssembledNode;
		if (mixedCompositeAssembledNode != null && mixedCompositeAssembledNode.m_progress == MixedCompositeOrderNode.MixingProgress.OverMixed)
		{
			SetNextIcon(_container, m_overmixedSprite);
			return;
		}
		foreach (AssembledDefinitionNode item in _order)
		{
			IngredientAssembledNode ingredientAssembledNode = item as IngredientAssembledNode;
			if (ingredientAssembledNode != null && ingredientAssembledNode.m_ingriedientOrderNode != null)
			{
				SetNextIcon(_container, ingredientAssembledNode.m_ingriedientOrderNode.m_iconSprite);
			}
			ItemAssembledNode itemAssembledNode = item as ItemAssembledNode;
			if (itemAssembledNode != null && itemAssembledNode.m_itemOrderNode != null)
			{
				SetNextIcon(_container, itemAssembledNode.m_itemOrderNode.m_iconSprite);
			}
		}
		SetRemainingIcons(_container, m_emptySprite);
	}

	private void SetRemainingIcons(GameObject _container, Sprite _sprite)
	{
		while (m_iconsInUse < m_minimumSize)
		{
			SetNextIcon(_container, _sprite);
		}
	}

	private void SetNextIcon(GameObject _container, Sprite _sprite)
	{
		if (m_icons.Count <= m_iconsInUse)
		{
			InstantiateIcon(_container);
		}
		m_icons[m_iconsInUse++].sprite = _sprite;
	}

	private void InstantiateIcon(GameObject _container)
	{
		GameObject obj = GameObjectUtils.CreateOnParent<Image>(_container, "Icon");
		Image image = obj.RequireComponent<Image>();
		image.sprite = m_emptySprite;
		m_icons.Add(image);
		UIUtils.SetupFillParentAreaRect(image.transform as RectTransform);
	}

	private void UpdateIconVisibility()
	{
		for (int i = 0; i < m_iconsInUse; i++)
		{
			m_icons[i].gameObject.SetActive(true);
		}
		for (int j = m_iconsInUse; j < m_icons.Count; j++)
		{
			m_icons[j].gameObject.SetActive(false);
		}
	}

	public void SetMinimumSize(int _size)
	{
		m_minimumSize = _size;
		RefreshSubElements();
	}
}
