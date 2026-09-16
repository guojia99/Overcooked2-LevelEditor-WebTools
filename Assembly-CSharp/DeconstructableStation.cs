using UnityEngine;

public class DeconstructableStation : MonoBehaviour, IHandleBuild
{
	public StationData StationData;

	public GameObject DeconstructedStationPrefab;

	private IFlowController m_flowController;

	public bool CanHandleBuild()
	{
		return true;
	}

	public void HandleBuild(PlayerControls _controls)
	{
		GameObject gameObject = Object.Instantiate(DeconstructedStationPrefab);
		gameObject.name = DeconstructedStationPrefab.name;
		gameObject.transform.SetParent(base.gameObject.transform.parent, false);
		gameObject.transform.position = base.gameObject.transform.position;
		gameObject.transform.rotation = base.gameObject.transform.rotation;
		base.gameObject.Destroy();
	}

	private void Awake()
	{
		m_flowController = GameUtils.GetFlowController();
	}
}
