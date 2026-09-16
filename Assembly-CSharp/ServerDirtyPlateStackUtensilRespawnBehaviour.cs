using UnityEngine;

public class ServerDirtyPlateStackUtensilRespawnBehaviour : ServerUtensilRespawnBehaviour
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
		ServerPlateReturnStation serverPlateReturnStation = _attachStation.gameObject.RequestComponent<ServerPlateReturnStation>();
		if (serverPlateReturnStation != null)
		{
			PlateReturnStation plateReturnStation = _attachStation.gameObject.RequireComponent<PlateReturnStation>();
			if (plateReturnStation.m_stackPrefab != null && plateReturnStation.m_stackPrefab.RequestComponent<DirtyPlateStack>() != null)
			{
				return (m_platingStepData == null || serverPlateReturnStation.GetPlatingStep() == m_platingStepData) && serverPlateReturnStation.CanReturnPlate();
			}
		}
		return false;
	}

	protected override ServerAttachStation GetFreeAttachStation()
	{
		if (m_IdealSpawnLocation != null && m_IdealSpawnLocation.enabled && m_IdealSpawnLocation.gameObject.activeInHierarchy && IsInLevelBounds(m_IdealSpawnLocation) && CanRespawnOnStation(m_IdealSpawnLocation))
		{
			return m_IdealSpawnLocation;
		}
		return base.GetFreeAttachStation();
	}

	protected override ServerAttachStation[] GetRespawnStations()
	{
		return Object.FindObjectsOfType<ServerPlateReturnStation>().ConvertAll((ServerPlateReturnStation _station) => _station.gameObject.RequireComponent<ServerAttachStation>());
	}

	protected override void AddItemToStation(ServerAttachStation _station)
	{
		ServerPlateReturnStation serverPlateReturnStation = _station.gameObject.RequestComponent<ServerPlateReturnStation>();
		if (serverPlateReturnStation == null)
		{
			base.AddItemToStation(_station);
			return;
		}
		ServerDirtyPlateStack serverDirtyPlateStack = base.gameObject.RequireComponent<ServerDirtyPlateStack>();
		for (int i = 0; i < serverDirtyPlateStack.GetSize(); i++)
		{
			serverPlateReturnStation.ReturnPlate();
		}
		NetworkUtils.DestroyObjectsRecursive(base.gameObject);
	}
}
