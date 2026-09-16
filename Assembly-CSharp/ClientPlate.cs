using System;
using System.Collections.Generic;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientPlate : ClientSynchroniserBase, IClientOrderDefinition
{
	public class IngredientContainerAdapter : IIngredientContents
	{
		private List<AssembledDefinitionNode> m_contents = new List<AssembledDefinitionNode>();

		public IngredientContainerAdapter(ClientIngredientContainer _container)
		{
			m_contents = new List<AssembledDefinitionNode>(_container.GetContentsCount());
			for (int i = 0; i < _container.GetContentsCount(); i++)
			{
				AssembledDefinitionNode contentsElement = _container.GetContentsElement(i);
				AssembledDefinitionNode item = contentsElement.Simpilfy();
				m_contents.Add(item);
			}
		}

		public bool CanAddIngredient(AssembledDefinitionNode _orderData)
		{
			return true;
		}

		public bool CanTakeContents(AssembledDefinitionNode[] _contents)
		{
			return true;
		}

		public void AddIngredient(AssembledDefinitionNode _orderData)
		{
			m_contents.Add(_orderData);
		}

		public AssembledDefinitionNode RemoveIngredient(int i)
		{
			AssembledDefinitionNode result = m_contents[i];
			m_contents.RemoveAt(i);
			return result;
		}

		public AssembledDefinitionNode GetContentsElement(int i)
		{
			return m_contents[i];
		}

		public int GetContentsCount()
		{
			return m_contents.Count;
		}

		public AssembledDefinitionNode[] GetContents()
		{
			return m_contents.ToArray();
		}

		public void Empty()
		{
			m_contents.Clear();
		}

		public bool HasContents()
		{
			return m_contents.Count != 0;
		}
	}

	private Plate m_plate;

	private OrderCompositionChangedCallback m_orderCompositionChangedCallbacks = delegate
	{
	};

	protected ClientIngredientContainer m_ingredientContainer;

	private ClientPlacementContainer m_placementContainer;

	private WaitForSeconds m_waitForPfxDelay;

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_plate = (Plate)synchronisedObject;
		ClientAnticipateInteractionHighlight clientAnticipateInteractionHighlight = base.gameObject.RequestComponent<ClientAnticipateInteractionHighlight>();
		if (clientAnticipateInteractionHighlight != null)
		{
			Transform parent = base.transform.parent;
			if (parent != null)
			{
				ClientAnticipateInteractionHighlight clientAnticipateInteractionHighlight2 = parent.gameObject.RequestComponentUpwardsRecursive<ClientAnticipateInteractionHighlight>();
				if (clientAnticipateInteractionHighlight2 != null)
				{
					clientAnticipateInteractionHighlight2.AddChild(clientAnticipateInteractionHighlight);
				}
			}
		}
		m_ingredientContainer = base.gameObject.GetComponent<ClientIngredientContainer>();
		m_ingredientContainer.RegisterContentsChangedCallback(OnContentsChanged);
		m_placementContainer = base.gameObject.RequireComponent<ClientPlacementContainer>();
		m_placementContainer.RegisterAllowItemPlacement(CanPlaceOnPlate);
	}

	public AssembledDefinitionNode GetOrderComposition()
	{
		return GetOrderComposition(m_ingredientContainer.GetContents());
	}

	public void RegisterOrderCompositionChangedCallback(OrderCompositionChangedCallback _callback)
	{
		m_orderCompositionChangedCallbacks = (OrderCompositionChangedCallback)Delegate.Combine(m_orderCompositionChangedCallbacks, _callback);
	}

	public void UnregisterOrderCompositionChangedCallback(OrderCompositionChangedCallback _callback)
	{
		m_orderCompositionChangedCallbacks = (OrderCompositionChangedCallback)Delegate.Remove(m_orderCompositionChangedCallbacks, _callback);
	}

	private void OnContentsChanged(AssembledDefinitionNode[] _contents)
	{
		m_orderCompositionChangedCallbacks(GetOrderComposition());
	}

	private AssembledDefinitionNode GetOrderComposition(AssembledDefinitionNode[] _contents)
	{
		return m_plate.GetOrderComposition(_contents);
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		if (null != m_placementContainer)
		{
			m_placementContainer.UnregisterAllowItemPlacement(CanPlaceOnPlate);
		}
	}

	protected virtual bool CanPlaceOnPlate(GameObject _gameObject, PlacementContext _context)
	{
		return m_plate.CanPlaceOnPlate(_gameObject, new IngredientContainerAdapter(m_ingredientContainer));
	}
}
