using System.Collections.Generic;
using UnityEngine;

public class ClientCleanPlateStack : ClientPlateStackBase, IClientHandlePlacement, IClientHandlePickup, IBaseHandlePlacement, IBaseHandlePickup
{
	private CleanPlateStack m_cleanPlateStack;

	private List<GameObject> m_delayedSetupPlates = new List<GameObject>();

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_cleanPlateStack = (CleanPlateStack)m_plateStack;
	}

	public override void UpdateSynchronising()
	{
		for (int num = m_delayedSetupPlates.Count - 1; num >= 0; num--)
		{
			DelayedPlateSetup(m_delayedSetupPlates[num]);
			m_delayedSetupPlates.RemoveAt(num);
		}
	}

	protected override void PlateSpawned(GameObject _object)
	{
		bool flag = false;
		if (m_stack.InspectTopOfStack() != null)
		{
			IIngredientContents ingredientContents = m_stack.InspectTopOfStack().RequestInterface<IIngredientContents>();
			flag = ingredientContents != null && ingredientContents.HasContents();
		}
		if (flag)
		{
			GameObject item = m_stack.RemoveFromStack();
			m_stack.AddToStack(_object);
			m_stack.AddToStack(item);
		}
		else
		{
			m_stack.AddToStack(_object);
		}
		m_delayedSetupPlates.Add(_object);
		NotifyPlateAdded(_object);
	}

	private void DelayedPlateSetup(GameObject _plate)
	{
		if (_plate != null && _plate.gameObject != null)
		{
			ClientHandlePickupReferral clientHandlePickupReferral = _plate.RequestComponent<ClientHandlePickupReferral>();
			if (clientHandlePickupReferral != null)
			{
				clientHandlePickupReferral.SetHandlePickupReferree(this);
			}
			ClientHandlePlacementReferral clientHandlePlacementReferral = _plate.RequestComponent<ClientHandlePlacementReferral>();
			if (clientHandlePlacementReferral != null)
			{
				clientHandlePlacementReferral.SetHandlePlacementReferree(this);
			}
		}
	}

	protected override void PlateRemoved()
	{
		GameObject gameObject = m_stack.RemoveFromStack();
		ClientHandlePickupReferral clientHandlePickupReferral = gameObject.RequestComponent<ClientHandlePickupReferral>();
		if (clientHandlePickupReferral != null && clientHandlePickupReferral.GetHandlePickupReferree() == this)
		{
			clientHandlePickupReferral.SetHandlePickupReferree(null);
		}
		ClientHandlePlacementReferral clientHandlePlacementReferral = gameObject.RequestComponent<ClientHandlePlacementReferral>();
		if (clientHandlePlacementReferral != null && clientHandlePlacementReferral.GetHandlePlacementReferree() == this)
		{
			clientHandlePlacementReferral.SetHandlePlacementReferree(null);
		}
		NotifyPlateRemoved(gameObject);
	}

	public bool CanHandlePickup(ICarrier _carrier)
	{
		return m_stack.GetSize() > 0;
	}

	public int GetPickupPriority()
	{
		return 0;
	}

	public bool CanHandlePlacement(ICarrier _carrier, Vector2 _directionXZ, PlacementContext _context)
	{
		GameObject gameObject = m_stack.InspectTopOfStack();
		if ((bool)gameObject)
		{
			IClientHandlePlacement clientHandlePlacement = gameObject.RequireInterface<IClientHandlePlacement>();
			return clientHandlePlacement.CanHandlePlacement(_carrier, _directionXZ, _context);
		}
		return false;
	}

	public int GetPlacementPriority()
	{
		return 0;
	}
}
