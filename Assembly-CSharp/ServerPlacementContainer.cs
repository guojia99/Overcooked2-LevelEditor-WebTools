using System.Collections.Generic;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerPlacementContainer : ServerSynchroniserBase, IHandlePlacement, IPlacementSupression, IBaseHandlePlacement
{
	protected ServerIngredientContainer m_ingredientContainer;

	protected PlacementContainer m_placementContainer;

	protected List<Generic<bool, GameObject, PlacementContext>> m_allowPlacementCallbacks = new List<Generic<bool, GameObject, PlacementContext>>();

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_ingredientContainer = base.gameObject.RequireComponent<ServerIngredientContainer>();
		m_placementContainer = (PlacementContainer)synchronisedObject;
	}

	public virtual int GetPlacementPriority()
	{
		return 0;
	}

	protected virtual bool CanCombine(GameObject _placingObject, PlacementContext _context)
	{
		return m_placementContainer.CanCombine(_placingObject, m_allowPlacementCallbacks, m_ingredientContainer, _context);
	}

	public virtual bool CanHandlePlacement(ICarrier _carrier, Vector2 _directionXZ, PlacementContext _context)
	{
		GameObject gameObject = _carrier.InspectCarriedItem();
		if (CanCombine(gameObject, _context))
		{
			return true;
		}
		ServerPlacementContainer serverPlacementContainer = gameObject.RequestComponent<ServerPlacementContainer>();
		if (serverPlacementContainer != null && serverPlacementContainer.CanCombine(base.gameObject, _context))
		{
			return true;
		}
		if (_carrier is ServerPlayerAttachmentCarrier)
		{
			ServerPreparationContainer serverPreparationContainer = gameObject.RequestComponent<ServerPreparationContainer>();
			if (serverPreparationContainer != null)
			{
				IOrderDefinition orderDefinition = base.gameObject.RequireInterface<IOrderDefinition>();
				if (serverPreparationContainer.CanAddOrderContents(new AssembledDefinitionNode[1] { orderDefinition.GetOrderComposition() }))
				{
					return true;
				}
			}
		}
		return false;
	}

	public virtual void HandlePlacement(ICarrier _carrier, Vector2 _directionXZ, PlacementContext _context)
	{
		GameObject gameObject = _carrier.InspectCarriedItem();
		if (CanCombine(gameObject, _context))
		{
			IContainerTransferBehaviour containerTransferBehaviour = gameObject.RequireInterface<IContainerTransferBehaviour>();
			containerTransferBehaviour.TransferToContainer(_carrier, m_ingredientContainer, false);
			m_ingredientContainer.InformOfInternalChange();
			return;
		}
		ServerIngredientContainer serverIngredientContainer = gameObject.RequireComponent<ServerIngredientContainer>();
		IContainerTransferBehaviour containerTransferBehaviour2 = base.gameObject.RequireInterface<IContainerTransferBehaviour>();
		if (containerTransferBehaviour2.CanTransferToContainer(serverIngredientContainer))
		{
			containerTransferBehaviour2.TransferToContainer(null, serverIngredientContainer, false);
			serverIngredientContainer.InformOfInternalChange();
		}
	}

	public void OnFailedToPlace(GameObject _item)
	{
	}

	public void RegisterAllowItemPlacement(Generic<bool, GameObject, PlacementContext> _allowPlacementCallback)
	{
		m_allowPlacementCallbacks.Add(_allowPlacementCallback);
	}

	public void UnregisterAllowItemPlacement(Generic<bool, GameObject, PlacementContext> _allowPlacementCallback)
	{
		m_allowPlacementCallbacks.Remove(_allowPlacementCallback);
	}
}
