public class ServerAvoidStationUtensilRespawnBehaviour : ServerUtensilRespawnBehaviour
{
	protected override bool CanRespawnOnStation(ServerAttachStation _attachStation)
	{
		return !_attachStation.CompareTag("CookingStation");
	}
}
