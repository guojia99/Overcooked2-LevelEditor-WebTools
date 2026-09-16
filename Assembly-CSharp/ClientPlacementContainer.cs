using System.Collections.Generic;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientPlacementContainer : ClientSynchroniserBase, IClientHandlePlacement, IBaseHandlePlacement
{
	private ClientIngredientContainer m_ingredientContainer;

	protected List<Generic<bool, GameObject, PlacementContext>> m_allowPlacementCallback = new List<Generic<bool, GameObject, PlacementContext>>();

	private PlacementContainer m_placementContainer;

	public void Awake()
	{
		m_placementContainer = base.gameObject.RequireComponent<PlacementContainer>();
		m_ingredientContainer = base.gameObject.RequireComponent<ClientIngredientContainer>();
	}

	public virtual bool CanHandlePlacement(ICarrier _carrier, Vector2 _directionXZ, PlacementContext _context)
	{
		GameObject gameObject = _carrier.InspectCarriedItem();
		if (CanCombine(gameObject, _context))
		{
			return true;
		}
		ClientPlacementContainer clientPlacementContainer = gameObject.RequestComponent<ClientPlacementContainer>();
		if (clientPlacementContainer != null && clientPlacementContainer.CanCombine(base.gameObject, _context))
		{
			return true;
		}
		return false;
	}

	public virtual int GetPlacementPriority()
	{
		return 0;
	}

	protected virtual bool CanCombine(GameObject _placingObject, PlacementContext _context)
	{
		return m_placementContainer.CanCombine(_placingObject, m_allowPlacementCallback, m_ingredientContainer, _context);
	}

	public void RegisterAllowItemPlacement(Generic<bool, GameObject, PlacementContext> _allowPlacementCallback)
	{
		m_allowPlacementCallback.Add(_allowPlacementCallback);
	}

	public void UnregisterAllowItemPlacement(Generic<bool, GameObject, PlacementContext> _allowPlacementCallback)
	{
		m_allowPlacementCallback.Remove(_allowPlacementCallback);
	}
}
