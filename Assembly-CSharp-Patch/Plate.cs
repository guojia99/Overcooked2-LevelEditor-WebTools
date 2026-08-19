using UnityEngine;

[AddComponentMenu("Scripts/Game/Environment/Plate")]
[RequireComponent(typeof(IngredientContainer))]
[RequireComponent(typeof(HandlePlacementReferral))]
[RequireComponent(typeof(PhysicalAttachment))]
[RequireComponent(typeof(PlacementContainer))]
public class Plate : MonoBehaviour
{
	[SerializeField]
	public PlatingStepData m_platingStep;

	private void Awake()
	{
		IngredientContainer ingredientContainer = GetComponent<IngredientContainer>();
		if (ingredientContainer != null)
		{
			ingredientContainer.m_capacity = 100;
		}
	}

	public bool CanPlaceOnPlate(GameObject _gameObject, IIngredientContents _ingredientContentsAdapter)
	{
		IContainerTransferBehaviour containerTransferBehaviour = _gameObject.RequestInterface<IContainerTransferBehaviour>();
		if (containerTransferBehaviour == null)
		{
			return false;
		}
		ItemCarrierAdapter carrier = new ItemCarrierAdapter(_gameObject);
		ServerIngredientContainer component = base.gameObject.GetComponent<ServerIngredientContainer>();
		if (component != null)
		{
			IIngredientContents component2 = component.GetComponent<IIngredientContents>();
			if (component2 != null && component2.GetContentsCount() == 1)
			{
				for (int i = 0; i < component2.GetContentsCount(); i++)
				{
					AssembledDefinitionNode assembledDefinitionNode = component2.GetContents()[i];
					if (assembledDefinitionNode.m_freeObject != null)
					{
						IHandleOrderModification handleOrderModification = assembledDefinitionNode.m_freeObject.RequestInterface<IHandleOrderModification>();
						IOrderDefinition orderDefinition = _gameObject.RequestInterface<IOrderDefinition>();
						if (handleOrderModification != null && orderDefinition != null)
						{
							return handleOrderModification.CanAddOrderContents(new AssembledDefinitionNode[1] { orderDefinition.GetOrderComposition() });
						}
					}
				}
			}
		}
		if (containerTransferBehaviour.CanTransferToContainer(_ingredientContentsAdapter))
		{
			containerTransferBehaviour.TransferToContainer(carrier, _ingredientContentsAdapter, true);
			AssembledDefinitionNode orderComposition = GetOrderComposition(_ingredientContentsAdapter.GetContents());
			return GameUtils.GetOrderPlatingPrefab(orderComposition, m_platingStep) != null;
		}
		return false;
	}

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
}
