using UnityEngine;

public class ServerPreventPlacement : MonoBehaviour, IHandlePlacement, IBaseHandlePlacement
{
	public bool CanHandlePlacement(ICarrier _carrier, Vector2 _directionXZ, PlacementContext _context)
	{
		return false;
	}

	public void HandlePlacement(ICarrier _carrier, Vector2 _directionXZ, PlacementContext _context)
	{
	}

	public void OnFailedToPlace(GameObject _item)
	{
	}

	public int GetPlacementPriority()
	{
		return 0;
	}
}
