using UnityEngine;

[AddComponentMenu("Scripts/Game/Environment/WashingStation")]
[RequireComponent(typeof(Interactable))]
public class WashingStation : MonoBehaviour
{
	[SerializeField]
	public PlateReturnStation m_dryingStation;

	[SerializeField]
	public float m_cleanPlateTime = 2f;

	[SerializeField]
	public ProgressUIController m_progressUIPrefab;

	[SerializeField]
	public GameObject[] m_dirtyPlates = new GameObject[0];

	public bool CanHandlePlacement(ICarrier _carrier, Vector2 _directionXZ, int _plateCount)
	{
		GameObject gameObject = _carrier.InspectCarriedItem();
		DirtyPlateStack dirtyPlateStack = ((!(gameObject != null)) ? null : gameObject.GetComponent<DirtyPlateStack>());

		// patch
		if (dirtyPlateStack != null)
		{
			GameObject m_stackPrefab = m_dryingStation.m_stackPrefab;
			if (m_stackPrefab != null)
			{
				GameObject m_platePrefab = m_stackPrefab.GetComponent<CleanPlateStack>().m_platePrefab;
				if (m_platePrefab != null)
				{
                    PlatingStepData platingStepData = m_platePrefab.GetComponent<Plate>().m_platingStep;
                    if (platingStepData != null && platingStepData == dirtyPlateStack.m_plateType)
                        return true;
                }
            }
        }
        return false;
		// patch
		//return dirtyPlateStack != null;
	}
}
