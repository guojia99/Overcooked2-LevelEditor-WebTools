using System;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientLadleContainer : ClientSynchroniserBase, IClientOrderDefinition
{
	private LadleContainer m_ladleContainer;

	private OrderCompositionChangedCallback m_orderCompositionChangedCallbacks = delegate
	{
	};

	private ClientIngredientContainer m_ingredientContainer;

	private ClientPlacementContainer m_PlacementContainer;

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_ladleContainer = (LadleContainer)synchronisedObject;
		m_ingredientContainer = base.gameObject.GetComponent<ClientIngredientContainer>();
		m_ingredientContainer.RegisterContentsChangedCallback(OnContentsChanged);
		m_PlacementContainer = base.gameObject.RequireComponent<ClientPlacementContainer>();
		m_PlacementContainer.RegisterAllowItemPlacement(AllowItemPlacement);
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

	private bool AllowItemPlacement(GameObject _object, PlacementContext _context)
	{
		return m_ladleContainer.AllowItemPlacement(_object, _context, m_ingredientContainer);
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
}
