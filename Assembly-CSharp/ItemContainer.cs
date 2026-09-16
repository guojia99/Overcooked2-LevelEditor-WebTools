using UnityEngine;

[RequireComponent(typeof(IngredientContainer))]
[RequireComponent(typeof(PlacementContainer))]
public class ItemContainer : MonoBehaviour
{
	[SerializeField]
	public OrderToPrefabLookup m_approvedContentsList;

	[SerializeField]
	public GameObject m_cosmeticsPrefab;

	private void Start()
	{
		if (m_approvedContentsList != null)
		{
			m_approvedContentsList.CacheAssembledOrderNodes();
		}
	}

	public AssembledDefinitionNode GetOrderComposition(IIngredientContents _ingredientContainer)
	{
		CompositeAssembledNode compositeAssembledNode = new CompositeAssembledNode();
		compositeAssembledNode.m_composition = _ingredientContainer.GetContents();
		return compositeAssembledNode;
	}

	public bool AllowItemPlacement(GameObject _object, PlacementContext _context)
	{
		IOrderDefinition orderDefinition = _object.RequestInterface<IOrderDefinition>();
		if (orderDefinition != null)
		{
			AssembledDefinitionNode orderComposition = orderDefinition.GetOrderComposition();
			if (orderComposition is ItemAssembledNode)
			{
				return m_approvedContentsList == null || m_approvedContentsList.GetPrefabForNode(orderComposition) != null;
			}
		}
		return false;
	}
}
