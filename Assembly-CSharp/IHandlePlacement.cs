using UnityEngine;

public interface IHandlePlacement : IBaseHandlePlacement
{
	void HandlePlacement(ICarrier _carrier, Vector2 _directionXZ, PlacementContext _context);

	void OnFailedToPlace(GameObject _item);
}
