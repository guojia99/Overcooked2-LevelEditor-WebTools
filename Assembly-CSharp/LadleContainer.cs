using UnityEngine;

[RequireComponent(typeof(IngredientContainer))]
[RequireComponent(typeof(PlacementContainer))]
[RequireComponent(typeof(PhysicalAttachment))]
public class LadleContainer : MonoBehaviour
{
	[SerializeField]
	private CookingStepData m_cookingStep;

	public AssembledDefinitionNode GetOrderComposition(AssembledDefinitionNode[] _contents)
	{
		if (_contents.Length == 1 && _contents[0].GetType() != typeof(IngredientAssembledNode))
		{
			return _contents[0];
		}
		CompositeAssembledNode compositeAssembledNode = new CompositeAssembledNode();
		compositeAssembledNode.m_composition = _contents;
		return compositeAssembledNode;
	}

	public bool AllowItemPlacement(GameObject _object, PlacementContext _context, IIngredientContents _contents)
	{
		IOrderDefinition orderDefinition = _object.RequestInterface<IOrderDefinition>();
		if (orderDefinition == null || _contents.HasContents())
		{
			return false;
		}
		CookingHandler cookingHandler = _object.RequestComponentRecursive<CookingHandler>();
		if (cookingHandler != null)
		{
			if (cookingHandler.m_cookingType != m_cookingStep)
			{
				return false;
			}
			CompositeAssembledNode compositeAssembledNode = orderDefinition.GetOrderComposition() as CompositeAssembledNode;
			if (compositeAssembledNode != null && !_contents.CanTakeContents(compositeAssembledNode.m_composition))
			{
				return false;
			}
			return true;
		}
		return false;
	}
}
