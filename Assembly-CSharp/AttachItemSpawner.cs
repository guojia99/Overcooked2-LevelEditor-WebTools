using UnityEngine;

[RequireComponent(typeof(HandlePlacementReferral))]
[RequireComponent(typeof(IOrderDefinition))]
public class AttachItemSpawner : MonoBehaviour, IContainerTransferBehaviour
{
	private IOrderDefinition m_orderDefinition;

	private void Awake()
	{
		m_orderDefinition = base.gameObject.RequireInterface<IOrderDefinition>();
	}

	public bool CanTransferToContainer(IIngredientContents _container)
	{
		return AssembledNodeTransfer.CanCombineWithContents(m_orderDefinition.GetOrderComposition(), _container);
	}

	public void TransferToContainer(ICarrierPlacement _carrier, IIngredientContents _container, bool _dontRemove)
	{
		AssembledDefinitionNode orderComposition = m_orderDefinition.GetOrderComposition();
		AssembledNodeTransfer.CombineWithContents(orderComposition, _container, _dontRemove);
	}
}
