using System;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerItemContainer : ServerSynchroniserBase, IOrderDefinition, IContainerTransferBehaviour
{
	private ItemContainer m_itemContainer;

	private ServerIngredientContainer m_ingredientContainer;

	private ServerPlacementContainer m_placementContainer;

	private ServerIngredientCatcher m_ingredientCatcher;

	private OrderCompositionChangedCallback m_orderCompositionChangedCallbacks = delegate
	{
	};

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_itemContainer = (ItemContainer)synchronisedObject;
		m_ingredientContainer = base.gameObject.GetComponent<ServerIngredientContainer>();
		m_ingredientContainer.RegisterContentsChangedCallback(OnContentsChanged);
		m_placementContainer = base.gameObject.RequireComponent<ServerPlacementContainer>();
		m_placementContainer.RegisterAllowItemPlacement(AllowItemPlacement);
		m_ingredientCatcher = base.gameObject.RequestComponent<ServerIngredientCatcher>();
		if (m_ingredientCatcher != null)
		{
			m_ingredientCatcher.RegisterAllowItemCatching(AllowItemCatching);
		}
	}

	public void RegisterOrderCompositionChangedCallback(OrderCompositionChangedCallback _callback)
	{
		m_orderCompositionChangedCallbacks = (OrderCompositionChangedCallback)Delegate.Combine(m_orderCompositionChangedCallbacks, _callback);
	}

	public void UnregisterOrderCompositionChangedCallback(OrderCompositionChangedCallback _callback)
	{
		m_orderCompositionChangedCallbacks = (OrderCompositionChangedCallback)Delegate.Remove(m_orderCompositionChangedCallbacks, _callback);
	}

	public override void OnDestroy()
	{
		base.OnDestroy();
		if (null != m_ingredientCatcher)
		{
			m_ingredientCatcher.UnregisterAllowItemCatching(AllowItemCatching);
		}
		if (null != m_placementContainer)
		{
			m_placementContainer.UnregisterAllowItemPlacement(AllowItemPlacement);
		}
		if (m_ingredientContainer != null)
		{
			m_ingredientContainer.UnregisterContentsChangedCallback(OnContentsChanged);
		}
	}

	private void OnContentsChanged(AssembledDefinitionNode[] _contents)
	{
		m_orderCompositionChangedCallbacks(GetOrderComposition());
	}

	private bool AllowItemPlacement(GameObject _object, PlacementContext _context)
	{
		return m_itemContainer.AllowItemPlacement(_object, _context);
	}

	private bool AllowItemCatching(GameObject _object)
	{
		return m_itemContainer.AllowItemPlacement(_object, new PlacementContext(PlacementContext.Source.Game));
	}

	public AssembledDefinitionNode GetOrderComposition()
	{
		return m_itemContainer.GetOrderComposition(m_ingredientContainer);
	}

	public bool CanTransferToContainer(IIngredientContents _container)
	{
		return AssembledNodeTransfer.CanTransferFromContainer(this, _container);
	}

	public void TransferToContainer(ICarrierPlacement _carrier, IIngredientContents _container, bool _dontRemove)
	{
		AssembledNodeTransfer.TransferFromContainer(this, _container, _dontRemove);
	}

	public bool CanTransferItemContents(AssembledDefinitionNode[] _toAdd)
	{
		return m_ingredientContainer.CanTakeContents(_toAdd);
	}
}
