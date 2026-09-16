using UnityEngine;

[RequireComponent(typeof(IOrderDefinition))]
public class ItemToContainerBehaviour : MonoBehaviour, IContainerTransferBehaviour, IPlaceUnder
{
	private IOrderDefinition m_orderDefinition;

	private void EnsureOrderDefinition()
	{
		if (m_orderDefinition == null)
		{
			m_orderDefinition = base.gameObject.RequestInterface<IOrderDefinition>();
		}
	}

	public bool CanTransferToContainer(IIngredientContents _container)
	{
		EnsureOrderDefinition();
		if (_container as MonoBehaviour != null)
		{
			GameObject obj = (_container as MonoBehaviour).gameObject;
			if (obj.RequestComponent<ItemContainer>() != null)
			{
				return AssembledNodeTransfer.CanCombineWithContents(m_orderDefinition.GetOrderComposition(), _container);
			}
		}
		return false;
	}

	public void TransferToContainer(ICarrierPlacement _carrier, IIngredientContents _container, bool _dontRemove)
	{
		EnsureOrderDefinition();
		AssembledNodeTransfer.CombineWithContents(m_orderDefinition.GetOrderComposition(), _container, _dontRemove);
		if (!_dontRemove)
		{
			_carrier.DestroyCarriedItem();
		}
	}
}
