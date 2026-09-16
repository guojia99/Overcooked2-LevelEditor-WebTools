using System;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientItemContainer : ClientSynchroniserBase, IClientOrderDefinition
{
	private ItemContainer m_itemContainer;

	private ClientIngredientContainer m_ingredientContainer;

	private ClientPlacementContainer m_placementContainer;

	private OrderCompositionChangedCallback m_orderCompositionChangedCallbacks = delegate
	{
	};

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_itemContainer = (ItemContainer)synchronisedObject;
		m_ingredientContainer = base.gameObject.GetComponent<ClientIngredientContainer>();
		m_ingredientContainer.RegisterContentsChangedCallback(OnContentsChanged);
		m_placementContainer = base.gameObject.RequireComponent<ClientPlacementContainer>();
		m_placementContainer.RegisterAllowItemPlacement(AllowItemPlacement);
		if (m_itemContainer.m_cosmeticsPrefab != null)
		{
			Transform parent = NetworkUtils.FindVisualRoot(base.gameObject);
			GameObject gameObject = UnityEngine.Object.Instantiate(m_itemContainer.m_cosmeticsPrefab, parent);
			if (gameObject != null)
			{
				gameObject.transform.localPosition = Vector3.zero;
				gameObject.transform.localRotation = Quaternion.identity;
			}
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

	public AssembledDefinitionNode GetOrderComposition()
	{
		return m_itemContainer.GetOrderComposition(m_ingredientContainer);
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
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
}
