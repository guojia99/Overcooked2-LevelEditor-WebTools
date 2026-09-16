using UnityEngine;

public class ServerCleanPlateStack : ServerPlateStackBase, IHandlePickup, IHandlePlacement, IBaseHandlePickup, IBaseHandlePlacement
{
	private CleanPlateStack m_cleanPlateStack;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_cleanPlateStack = (CleanPlateStack)m_plateStack;
	}

	public override void AddToStack()
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
			AddToBaseStack();
			m_stack.AddToStack(item);
		}
		else
		{
			AddToBaseStack();
		}
		GameObject obj = m_stack.InspectTopOfStack();
		ServerHandlePickupReferral serverHandlePickupReferral = obj.RequestComponent<ServerHandlePickupReferral>();
		if (serverHandlePickupReferral != null)
		{
			serverHandlePickupReferral.SetHandlePickupReferree(this);
		}
		ServerHandlePlacementReferral serverHandlePlacementReferral = obj.RequestComponent<ServerHandlePlacementReferral>();
		if (serverHandlePlacementReferral != null)
		{
			serverHandlePlacementReferral.SetHandlePlacementReferree(this);
		}
	}

	private void AddToBaseStack()
	{
		base.AddToStack();
		GameObject obj = m_stack.InspectTopOfStack();
		ServerUtensilRespawnBehaviour serverUtensilRespawnBehaviour = base.gameObject.RequestComponent<ServerUtensilRespawnBehaviour>();
		ServerUtensilRespawnBehaviour serverUtensilRespawnBehaviour2 = obj.RequestComponent<ServerUtensilRespawnBehaviour>();
		if (serverUtensilRespawnBehaviour != null && serverUtensilRespawnBehaviour2 != null)
		{
			serverUtensilRespawnBehaviour2.SetIdealRespawnLocation(serverUtensilRespawnBehaviour.GetIdealRespawnLocation());
		}
	}

	protected override GameObject RemoveFromStack()
	{
		GameObject gameObject = base.RemoveFromStack();
		ServerHandlePickupReferral serverHandlePickupReferral = gameObject.RequestComponent<ServerHandlePickupReferral>();
		if (serverHandlePickupReferral != null && serverHandlePickupReferral.GetHandlePickupReferree() == this)
		{
			serverHandlePickupReferral.SetHandlePickupReferree(null);
		}
		ServerHandlePlacementReferral serverHandlePlacementReferral = gameObject.RequestComponent<ServerHandlePlacementReferral>();
		if (serverHandlePlacementReferral != null && serverHandlePlacementReferral.GetHandlePlacementReferree() == this)
		{
			serverHandlePlacementReferral.SetHandlePlacementReferree(null);
		}
		return gameObject;
	}

	public bool CanHandlePickup(ICarrier _carrier)
	{
		return m_stack.GetSize() > 0;
	}

	public void HandlePickup(ICarrier _carrier, Vector2 _directionXZ)
	{
		GameObject gameObject = RemoveFromStack();
		_carrier.CarryItem(gameObject);
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
			IHandlePlacement handlePlacement = gameObject.RequireInterface<IHandlePlacement>();
			return handlePlacement.CanHandlePlacement(_carrier, _directionXZ, _context);
		}
		return false;
	}

	public void HandlePlacement(ICarrier _carrier, Vector2 _directionXZ, PlacementContext _context)
	{
		GameObject obj = m_stack.InspectTopOfStack();
		IHandlePlacement handlePlacement = obj.RequireInterface<IHandlePlacement>();
		handlePlacement.HandlePlacement(_carrier, _directionXZ, _context);
	}

	public void OnFailedToPlace(GameObject _item)
	{
	}

	public int GetPlacementPriority()
	{
		return 0;
	}
}
