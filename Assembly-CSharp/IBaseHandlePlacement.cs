using UnityEngine;

public interface IBaseHandlePlacement
{
	bool CanHandlePlacement(ICarrier _carrier, Vector2 _directionXZ, PlacementContext _context);

	int GetPlacementPriority();
}
