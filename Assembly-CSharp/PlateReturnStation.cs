using UnityEngine;

[AddComponentMenu("Scripts/Game/Environment/PlateReturnStation")]
[RequireComponent(typeof(AttachStation))]
public class PlateReturnStation : MonoBehaviour
{
	[SerializeField]
	public GameObject m_stackPrefab;

	[SerializeField]
	public int m_startingPlateNumber;
}
