using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientPlacementInteractable : ClientSynchroniserBase, IClientHandlePlacement, IBaseHandlePlacement
{
	public bool CanHandlePlacement(ICarrier _carrier, Vector2 _directionXZ, PlacementContext _context)
	{
		return true;
	}

	public int GetPlacementPriority()
	{
		return 1;
	}
}
