using UnityEngine;

public class ItemHeatTransferBehaviour : MonoBehaviour, IHeatTransferBehaviour
{
	private IOrderDefinition m_orderDefinition;

	private void EnsureOrderDefinition()
	{
		if (m_orderDefinition == null)
		{
			m_orderDefinition = base.gameObject.RequestInterface<IOrderDefinition>();
		}
	}

	public bool CanTransferToContainer(IHeatContainer _container)
	{
		EnsureOrderDefinition();
		AssembledDefinitionNode orderComposition = m_orderDefinition.GetOrderComposition();
		if (orderComposition is ItemAssembledNode)
		{
			return (orderComposition as ItemAssembledNode).m_itemOrderNode.m_heatValue > 0f;
		}
		return false;
	}

	public void TransferToContainer(ICarrierPlacement _carrier, IHeatContainer _container)
	{
		EnsureOrderDefinition();
		AssembledDefinitionNode orderComposition = m_orderDefinition.GetOrderComposition();
		if (orderComposition is ItemAssembledNode)
		{
			ItemAssembledNode itemAssembledNode = orderComposition as ItemAssembledNode;
			_container.IncreaseHeat(itemAssembledNode.m_itemOrderNode.m_heatValue);
		}
		_carrier.DestroyCarriedItem();
	}
}
