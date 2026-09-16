using UnityEngine;

[RequireComponent(typeof(IOrderDefinition))]
public class IngredientToContainerBehaviour : MonoBehaviour, IContainerTransferBehaviour, IPlaceUnder
{
	private IOrderDefinition m_orderDefinition;

	public bool CanTransferToContainer(IIngredientContents _container)
	{
		EnsureOrderDefinition();
		return AssembledNodeTransfer.CanCombineWithContents(m_orderDefinition.GetOrderComposition(), _container);
	}

	public void TransferToContainer(ICarrierPlacement _carrier, IIngredientContents _container, bool _dontRemove)
	{
		EnsureOrderDefinition();
		AssembledDefinitionNode assembledDefinitionNode = m_orderDefinition.GetOrderComposition();
		if (_dontRemove)
		{
			assembledDefinitionNode = assembledDefinitionNode.Simpilfy();
		}
		AssembledNodeTransfer.CombineWithContents(assembledDefinitionNode, _container, _dontRemove);
		if (!_dontRemove)
		{
			_carrier.DestroyCarriedItem();
		}
	}

	private void EnsureOrderDefinition()
	{
		if (m_orderDefinition == null)
		{
			m_orderDefinition = base.gameObject.RequestInterface<IOrderDefinition>();
		}
	}
}
