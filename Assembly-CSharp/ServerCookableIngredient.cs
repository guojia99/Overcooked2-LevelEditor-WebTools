using System;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerCookableIngredient : ServerSynchroniserBase, IOrderDefinition
{
	private CookableIngredient m_cookableIngredient;

	private ServerCookingHandler m_cookingHandler;

	private OrderCompositionChangedCallback m_orderCompositionChangedCallbacks = delegate
	{
	};

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_cookableIngredient = (CookableIngredient)synchronisedObject;
		m_cookingHandler = base.gameObject.RequireComponent<ServerCookingHandler>();
		m_cookingHandler.CookingStateChangedCallback += delegate
		{
			m_orderCompositionChangedCallbacks(GetOrderComposition());
		};
	}

	public AssembledDefinitionNode GetOrderComposition()
	{
		CookedCompositeAssembledNode cookedCompositeAssembledNode = new CookedCompositeAssembledNode();
		IngredientAssembledNode ingredientAssembledNode = new IngredientAssembledNode(m_cookableIngredient.m_ingredientOrderNode);
		cookedCompositeAssembledNode.m_composition = new AssembledDefinitionNode[1] { ingredientAssembledNode };
		cookedCompositeAssembledNode.m_cookingStep = m_cookingHandler.AccessCookingType;
		cookedCompositeAssembledNode.m_recordedProgress = m_cookingHandler.GetCookingProgress() / m_cookingHandler.AccessCookingTime;
		cookedCompositeAssembledNode.m_progress = m_cookingHandler.GetCookedOrderState();
		return cookedCompositeAssembledNode;
	}

	public void RegisterOrderCompositionChangedCallback(OrderCompositionChangedCallback _callback)
	{
		m_orderCompositionChangedCallbacks = (OrderCompositionChangedCallback)Delegate.Combine(m_orderCompositionChangedCallbacks, _callback);
	}

	public void UnregisterOrderCompositionChangedCallback(OrderCompositionChangedCallback _callback)
	{
		m_orderCompositionChangedCallbacks = (OrderCompositionChangedCallback)Delegate.Remove(m_orderCompositionChangedCallbacks, _callback);
	}
}
