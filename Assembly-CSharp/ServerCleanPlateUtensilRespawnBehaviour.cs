using UnityEngine;

public class ServerCleanPlateUtensilRespawnBehaviour : ServerUtensilRespawnBehaviour
{
	private PlatingStepData m_platingStepData;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		ServerPlateStackBase serverPlateStackBase = base.gameObject.RequestComponent<ServerPlateStackBase>();
		if (serverPlateStackBase != null)
		{
			m_platingStepData = serverPlateStackBase.GetPlatingStep();
		}
	}

	protected override bool CanRespawnOnStation(ServerAttachStation _attachStation)
	{
		if (_attachStation.CompareTag("CookingStation") || _attachStation.CompareTag("PlateStation") || _attachStation.gameObject.RequestComponent<RubbishBin>() != null || _attachStation.gameObject.RequestComponent<ConveyorStation>() != null || _attachStation.gameObject.RequestComponent<WashingStation>() != null)
		{
			return false;
		}
		ServerPlateReturnStation serverPlateReturnStation = _attachStation.gameObject.RequestComponent<ServerPlateReturnStation>();
		if (serverPlateReturnStation != null)
		{
			PlateReturnStation plateReturnStation = _attachStation.gameObject.RequireComponent<PlateReturnStation>();
			if (plateReturnStation.m_stackPrefab != null && plateReturnStation.m_stackPrefab.RequestComponent<CleanPlateStack>() != null)
			{
				return (m_platingStepData == null || serverPlateReturnStation.GetPlatingStep() == m_platingStepData) && serverPlateReturnStation.CanReturnPlate();
			}
		}
		return _attachStation.CanAttachToSelf(base.gameObject);
	}

	protected override void AddItemToStation(ServerAttachStation _station)
	{
		ServerPlateReturnStation serverPlateReturnStation = _station.gameObject.RequestComponent<ServerPlateReturnStation>();
		if (serverPlateReturnStation == null)
		{
			base.AddItemToStation(_station);
			return;
		}
		ServerPlate serverPlate = base.gameObject.RequestComponent<ServerPlate>();
		if (serverPlate != null)
		{
			serverPlateReturnStation.ReturnPlate();
		}
		NetworkUtils.DestroyObjectsRecursive(base.gameObject);
	}
}
