using System;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientMixableContainer : ClientSynchroniserBase, IClientOrderDefinition
{
	private MixableContainer m_mixableContainer;

	private OrderCompositionChangedCallback m_orderCompositionChangedCallbacks = delegate
	{
	};

	private ClientIngredientContainer m_itemContainer;

	private ClientMixingHandler m_mixingHandler;

	private ClientPlacementContainer m_PlacementContainer;

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_mixableContainer = (MixableContainer)synchronisedObject;
		m_mixingHandler = base.gameObject.RequireComponent<ClientMixingHandler>();
		ClientMixingHandler mixingHandler = m_mixingHandler;
		mixingHandler.m_stateChangedCallback = (StateChanged)Delegate.Combine(mixingHandler.m_stateChangedCallback, (StateChanged)delegate
		{
			m_orderCompositionChangedCallbacks(GetOrderComposition());
		});
		m_itemContainer = base.gameObject.GetComponent<ClientIngredientContainer>();
		m_itemContainer.RegisterContentsChangedCallback(OnContentsChanged);
		m_PlacementContainer = base.gameObject.RequireComponent<ClientPlacementContainer>();
		m_PlacementContainer.RegisterAllowItemPlacement(AllowItemPlacement);
	}

	public AssembledDefinitionNode GetOrderComposition()
	{
		return m_mixableContainer.GetOrderComposition(m_itemContainer, m_mixingHandler.GetMixingProgress() / m_mixingHandler.AccessMixingTime, m_mixingHandler.GetMixedOrderState());
	}

	public void RegisterOrderCompositionChangedCallback(OrderCompositionChangedCallback _callback)
	{
		m_orderCompositionChangedCallbacks = (OrderCompositionChangedCallback)Delegate.Combine(m_orderCompositionChangedCallbacks, _callback);
	}

	public void UnregisterOrderCompositionChangedCallback(OrderCompositionChangedCallback _callback)
	{
		m_orderCompositionChangedCallbacks = (OrderCompositionChangedCallback)Delegate.Remove(m_orderCompositionChangedCallbacks, _callback);
	}

	private bool AllowItemPlacement(GameObject _object, PlacementContext _context)
	{
		float cookingProgress = 0f;
		ClientCookableContainer clientCookableContainer = base.gameObject.RequestComponent<ClientCookableContainer>();
		if (clientCookableContainer != null)
		{
			ClientCookingHandler cookingHandler = clientCookableContainer.GetCookingHandler();
			if (cookingHandler != null)
			{
				cookingProgress = cookingHandler.GetCookingProgress();
			}
		}
		return m_mixableContainer.AllowItemPlacement(_object, _context, m_mixableContainer.m_ApprovedIngredients, m_mixingHandler.IsOverMixed(), cookingProgress);
	}

	private bool AllowItemCatching(GameObject _object)
	{
		return AllowItemPlacement(_object, default(PlacementContext));
	}

	private void OnContentsChanged(AssembledDefinitionNode[] _contents)
	{
		m_orderCompositionChangedCallbacks(GetOrderComposition());
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		if (m_PlacementContainer != null)
		{
			m_PlacementContainer.UnregisterAllowItemPlacement(AllowItemPlacement);
		}
	}

	public ClientMixingHandler GetMixingHandler()
	{
		return m_mixingHandler;
	}
}
