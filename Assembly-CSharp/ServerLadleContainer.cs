using System;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerLadleContainer : ServerSynchroniserBase, IOrderDefinition, IContainerTransferBehaviour
{
	private LadleContainer m_ladleContainer;

	private ServerIngredientContainer m_ingredientContainer;

	private ServerPlacementContainer m_placementContainer;

	private OrderCompositionChangedCallback m_orderCompositionChangedCallbacks = delegate
	{
	};

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_ladleContainer = (LadleContainer)synchronisedObject;
		m_placementContainer = base.gameObject.RequireComponent<ServerPlacementContainer>();
		m_placementContainer.RegisterAllowItemPlacement(AllowItemPlacement);
		m_ingredientContainer = base.gameObject.GetComponent<ServerIngredientContainer>();
		m_ingredientContainer.RegisterContentsChangedCallback(OnContentsChanged);
	}

	private bool AllowItemPlacement(GameObject _object, PlacementContext _context)
	{
		return m_ladleContainer.AllowItemPlacement(_object, _context, m_ingredientContainer);
	}

	public void OnContentsChanged(AssembledDefinitionNode[] _contents)
	{
		m_orderCompositionChangedCallbacks(GetOrderComposition());
	}

	public AssembledDefinitionNode GetOrderComposition()
	{
		return m_ladleContainer.GetOrderComposition(m_ingredientContainer.GetContents());
	}

	public void RegisterOrderCompositionChangedCallback(OrderCompositionChangedCallback _callback)
	{
		m_orderCompositionChangedCallbacks = (OrderCompositionChangedCallback)Delegate.Combine(m_orderCompositionChangedCallbacks, _callback);
	}

	public void UnregisterOrderCompositionChangedCallback(OrderCompositionChangedCallback _callback)
	{
		m_orderCompositionChangedCallbacks = (OrderCompositionChangedCallback)Delegate.Remove(m_orderCompositionChangedCallbacks, _callback);
	}

	public void TransferToContainer(ICarrierPlacement _carrier, IIngredientContents _plateContainer, bool _dontRemove)
	{
		AssembledNodeTransfer.TransferFromContainer(this, _plateContainer, _dontRemove);
	}

	public bool CanTransferToContainer(IIngredientContents _container)
	{
		return AssembledNodeTransfer.CanTransferFromContainer(this, _container);
	}

	public override void OnDestroy()
	{
		base.OnDestroy();
		if (m_placementContainer != null)
		{
			m_placementContainer.UnregisterAllowItemPlacement(AllowItemPlacement);
		}
	}
}
