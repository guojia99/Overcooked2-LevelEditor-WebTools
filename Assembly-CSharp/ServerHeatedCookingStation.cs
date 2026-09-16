using UnityEngine;

public class ServerHeatedCookingStation : ServerCookingStation
{
	private HeatedCookingStation m_heatedCookingStation;

	private ServerHeatedStation m_heatedStation;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_heatedCookingStation = (HeatedCookingStation)synchronisedObject;
		if (m_heatedCookingStation.m_heatSource != null)
		{
			m_heatedStation = m_heatedCookingStation.m_heatSource.gameObject.RequireComponent<ServerHeatedStation>();
		}
		else
		{
			m_heatedStation = base.gameObject.RequireComponent<ServerHeatedStation>();
		}
		m_heatedStation.RegisterHeatRangeChangedCallback(OnHeatRangeChanged);
		OnHeatRangeChanged(HeatRange.Low);
	}

	public override void OnDestroy()
	{
		base.OnDestroy();
		if (m_heatedStation != null)
		{
			m_heatedStation.UnregisterHeatRangeChangedCallback(OnHeatRangeChanged);
		}
	}

	private void OnHeatRangeChanged(HeatRange _range)
	{
		switch (_range)
		{
		case HeatRange.High:
			m_cookingSpeed = m_heatedCookingStation.m_cookingSpeedHigh;
			break;
		case HeatRange.Moderate:
			m_cookingSpeed = m_heatedCookingStation.m_cookingSpeedModerate;
			break;
		case HeatRange.Low:
			m_cookingSpeed = m_heatedCookingStation.m_cookingSpeedLow;
			break;
		}
	}
}
