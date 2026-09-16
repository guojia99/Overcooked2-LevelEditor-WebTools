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
		return dirtyPlateStack != null;
	}
}
