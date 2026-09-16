using UnityEngine;

public class IngredientPropertiesComponent : MonoBehaviour, ISpawnableItem, IOrderDefinition, IClientOrderDefinition
{
	[SerializeField]
	private IngredientOrderNode m_ingredientOrderNode;

	public void Awake()
	{
	}

	public SubTexture2D GetSubTexture()
	{
		return m_ingredientOrderNode.m_crateLid;
	}

	public Sprite GetUIIcon()
	{
		return m_ingredientOrderNode.m_iconSprite;
	}

	public AssembledDefinitionNode GetOrderComposition()
	{
		return new IngredientAssembledNode(m_ingredientOrderNode);
	}

	public void RegisterOrderCompositionChangedCallback(OrderCompositionChangedCallback _callback)
	{
	}

	public void UnregisterOrderCompositionChangedCallback(OrderCompositionChangedCallback _callback)
	{
	}

	public void SetIngredientOrderNode(IngredientOrderNode ingredientOrderNode)
	{
		m_ingredientOrderNode = ingredientOrderNode;
	}
}
