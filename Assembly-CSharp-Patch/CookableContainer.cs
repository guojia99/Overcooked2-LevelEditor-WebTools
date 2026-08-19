using UnityEngine;

[AddComponentMenu("Scripts/Game/Environment/CookableContainer")]
[RequireComponent(typeof(IngredientContainer))]
[RequireComponent(typeof(PlacementContainer))]
[RequireComponent(typeof(CookingHandler))]
public class CookableContainer : MonoBehaviour
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

	public AssembledDefinitionNode GetOrderComposition(IIngredientContents _itemContainer, IBaseCookable _cookingHandler, AssembledDefinitionNode _cookableMixableContents, bool _isMixed)
	{
		CookedCompositeAssembledNode cookedCompositeAssembledNode = new CookedCompositeAssembledNode();
		if (_cookableMixableContents != null)
		{
			if (!_isMixed)
			{
				return _cookableMixableContents;
			}
			cookedCompositeAssembledNode.m_composition = new AssembledDefinitionNode[1] { _cookableMixableContents };
		}
		else
		{
			cookedCompositeAssembledNode.m_composition = _itemContainer.GetContents();
		}
		cookedCompositeAssembledNode.m_cookingStep = _cookingHandler.AccessCookingType;
		cookedCompositeAssembledNode.m_recordedProgress = _cookingHandler.GetCookingProgress() / _cookingHandler.AccessCookingTime;
		cookedCompositeAssembledNode.m_progress = _cookingHandler.GetCookedOrderState();
		return cookedCompositeAssembledNode;
	}

	public bool AllowItemPlacement(GameObject _object, PlacementContext _context, IBaseCookable _iCookingHandler)
	{
		// patch
		//CookableProperties cookableProperties = _object.RequestComponent<CookableProperties>();
		//if (cookableProperties == null || !cookableProperties.AllowsCookingStep(_iCookingHandler.AccessCookingType))
		//{
		//	return false;
		//}
		//if (m_approvedContentsList != null)
		//{
		//	IOrderDefinition orderDefinition = _object.RequestInterface<IOrderDefinition>();
		//	if (orderDefinition != null && m_approvedContentsList.GetPrefabForNode(orderDefinition.GetOrderComposition()) == null)
		//	{
		//		return false;
		//	}
		//}
		//return !_iCookingHandler.IsBurning();


		if (m_approvedContentsList == null || _iCookingHandler.IsBurning()) 
			return false;
        IOrderDefinition orderDefinition = _object.RequestInterface<IOrderDefinition>();
        if (orderDefinition == null) 
			return false;

		// different utensil type && cooked -> false
		AssembledDefinitionNode assembledDefinitionNode = orderDefinition.GetOrderComposition();
		if (assembledDefinitionNode is CookedCompositeAssembledNode)
		{
			CookedCompositeAssembledNode cookedCompositeAssembledNode = assembledDefinitionNode as CookedCompositeAssembledNode;
			if (cookedCompositeAssembledNode.m_progress == CookedCompositeOrderNode.CookingProgress.Burnt)
				return false;
			if (cookedCompositeAssembledNode.m_progress == CookedCompositeOrderNode.CookingProgress.Cooked &&
				cookedCompositeAssembledNode.m_cookingStep.m_uID != _iCookingHandler.AccessCookingType.m_uID)
				return m_approvedContentsList.GetPrefabForNode(cookedCompositeAssembledNode) != null;
		}
		if (assembledDefinitionNode is CompositeAssembledNode)
		{
            CompositeAssembledNode compositeAssembledNode = assembledDefinitionNode as CompositeAssembledNode;
			foreach (AssembledDefinitionNode composition in compositeAssembledNode.m_composition)
			{
				if (m_approvedContentsList.GetPrefabForNode(composition) == null)
					return false;
			}
            return true;
		}

		return m_approvedContentsList.GetPrefabForNode(orderDefinition.GetOrderComposition()) != null;
		// patch
	}
}
