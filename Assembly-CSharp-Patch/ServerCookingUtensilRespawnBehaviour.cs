using LevelEditor;

public class ServerCookingUtensilRespawnBehaviour : ServerUtensilRespawnBehaviour
{
	private const float c_incorrectStationModifier = 1000f;

	private CookingHandler m_cookingHandler;

	private MixingHandler m_mixingHandler;

	private void Awake()
	{
		m_cookingHandler = base.gameObject.RequestComponent<CookingHandler>();
		m_mixingHandler = base.gameObject.RequestComponent<MixingHandler>();
	}

	public override float GetStationRespawnPriority(ServerAttachStation _station)
	{
		return GetRespawnDistance(_station.transform.position) + ((!_station.CompareTag("CookingStation")) ? 1000f : 0f);
	}

	protected override bool CanRespawnOnStation(ServerAttachStation _attachStation)
	{
		if (_attachStation.CompareTag("PlateReturn") || _attachStation.CompareTag("PlateStation") || _attachStation.gameObject.RequestComponent<RubbishBin>() != null || _attachStation.gameObject.RequestComponent<ConveyorStation>() != null || _attachStation.gameObject.RequestComponent<WashingStation>() != null)
		{
			return false;
		}
		if (_attachStation.CompareTag("CookingStation") && InvalidStationFilter(_attachStation))
		{
			return false;
		}
		return _attachStation.CanAttachToSelf(base.gameObject);
	}

	private bool InvalidStationFilter(ServerAttachStation _station)
	{
		if (m_mixingHandler != null)
		{
			return _station.gameObject.RequestComponent<MixingStation>() == null;
		}
		if (m_cookingHandler != null)
		{
			CookingStation cookingStation = _station.gameObject.RequestComponent<CookingStation>();
			// patch
			//return cookingStation == null || cookingStation.m_stationType != m_cookingHandler.m_stationType;
			if (cookingStation == null) return true;
			if (cookingStation.m_stationType == m_cookingHandler.m_stationType) return false;
            MultiCookingStationTypes multiCookingStationTypes = GetComponent<MultiCookingStationTypes>();
			return multiCookingStationTypes == null || !multiCookingStationTypes.MatchType(cookingStation.m_stationType);
            // patch
        }
		return false;
	}
}
