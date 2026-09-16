using UnityEngine;

public class ItemPropertiesComponent : MonoBehaviour, ISpawnableItem, IOrderDefinition, IClientOrderDefinition
{
	[SerializeField]
	private ItemOrderNode m_itemDefinition;

	public SubTexture2D GetSubTexture()
	{
		return m_itemDefinition.m_crateLid;
	}

	public Sprite GetUIIcon()
	{
		return m_itemDefinition.m_iconSprite;
	}

	public AssembledDefinitionNode GetOrderComposition()
	{
		return new ItemAssembledNode(m_itemDefinition);
	}

	public void RegisterOrderCompositionChangedCallback(OrderCompositionChangedCallback _callback)
	{
	}

	public void UnregisterOrderCompositionChangedCallback(OrderCompositionChangedCallback _callback)
	{
	}
}
