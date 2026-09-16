using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlateReturnController
{
	public struct PlateReturnControllerConfig
	{
		public float m_plateReturnTime;
	}

	private class PlatesPendingReturn
	{
		public ServerPlateReturnStation m_station;

		public float m_timer;

		public PlatingStepData m_platingStepData;

		public PlatesPendingReturn(ServerPlateReturnStation _returnStation, float _delay, PlatingStepData platingStepData)
		{
			m_station = _returnStation;
			m_timer = _delay;
			m_platingStepData = platingStepData;
		}
	}

	private PlateReturnControllerConfig m_plateReturnControllerDesc;

	private FastList<ServerPlateReturnStation> m_plateReturnStations = new FastList<ServerPlateReturnStation>();

	private FastList<PlatesPendingReturn> m_platesToReturn = new FastList<PlatesPendingReturn>();

	private LevelConfigBase m_levelConfig;

	public PlateReturnController(ref PlateReturnControllerConfig _plateReturnControllerDesc)
	{
		m_plateReturnControllerDesc = _plateReturnControllerDesc;
	}

	public void Init()
	{
		m_levelConfig = GameUtils.GetLevelConfig();
		m_platesToReturn.Clear();
		m_plateReturnStations.Clear();
		GameObject[] rootGameObjects = SceneManager.GetActiveScene().GetRootGameObjects();
		for (int i = 0; i < rootGameObjects.Length; i++)
		{
			ServerPlateReturnStation[] array = rootGameObjects[i].RequestComponentsRecursive<ServerPlateReturnStation>();
			for (int j = 0; j < array.Length; j++)
			{
				MonoBehaviour monoBehaviour = array[j];
				if (monoBehaviour.CompareTag("PlateReturn"))
				{
					m_plateReturnStations.Add(array[j]);
				}
			}
		}
	}

	public void FoodDelivered(AssembledDefinitionNode _definition, PlatingStepData _plateType, ServerPlateStation _station)
	{
		ServerPlateReturnStation returnStation = _station.GetReturnStation(_plateType);
		if (returnStation != null)
		{
			m_platesToReturn.Add(new PlatesPendingReturn(returnStation, m_plateReturnControllerDesc.m_plateReturnTime, _plateType));
		}
		else if (m_plateReturnStations.Count > 0)
		{
			returnStation = FindBestReturnStation(_plateType);
			m_platesToReturn.Add(new PlatesPendingReturn(returnStation, m_plateReturnControllerDesc.m_plateReturnTime, _plateType));
		}
	}

	public void Update()
	{
		for (int num = m_platesToReturn.Count - 1; num >= 0; num--)
		{
			PlatesPendingReturn platesPendingReturn = m_platesToReturn._items[num];
			platesPendingReturn.m_timer -= TimeManager.GetDeltaTime(LayerMask.NameToLayer("Default"));
			if (platesPendingReturn.m_timer < 0f)
			{
				if (platesPendingReturn.m_station == null || platesPendingReturn.m_station.gameObject == null || !platesPendingReturn.m_station.gameObject.activeInHierarchy || !IsInLevelBounds(platesPendingReturn.m_station))
				{
					platesPendingReturn.m_station = FindBestReturnStation(platesPendingReturn.m_platingStepData);
				}
				else if (platesPendingReturn.m_station.CanReturnPlate())
				{
					platesPendingReturn.m_station.ReturnPlate();
					m_platesToReturn.RemoveAt(num);
				}
			}
		}
	}

	protected bool IsInLevelBounds(ServerPlateReturnStation _station)
	{
		return (m_levelConfig != null && !m_levelConfig.m_enableRespawnBounds) || LevelBounds.ActiveBoundsContain(_station.transform.position);
	}

	private ServerPlateReturnStation FindBestReturnStation(PlatingStepData _plateType)
	{
		if (m_plateReturnStations.Count > 0)
		{
			return m_plateReturnStations.Find((ServerPlateReturnStation x) => x.GetPlatingStep() == _plateType && x.gameObject.activeInHierarchy && IsInLevelBounds(x));
		}
		return null;
	}
}
