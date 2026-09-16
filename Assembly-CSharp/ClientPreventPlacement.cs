using UnityEngine;

public class ClientPreventPlacement : MonoBehaviour, IClientHandlePlacement, IBaseHandlePlacement
{
	public bool CanHandlePlacement(ICarrier _carrier, Vector2 _directionXZ, PlacementContext _context)
	{
		return false;
	}

	public int GetPlacementPriority()
	{
		return 0;
	}
}
