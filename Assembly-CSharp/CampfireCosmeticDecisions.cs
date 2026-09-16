using UnityEngine;

[RequireComponent(typeof(AttachStation))]
[RequireComponent(typeof(HeatedCookingStation))]
public class CampfireCosmeticDecisions : MonoBehaviour
{
	[SerializeField]
	[AssignChildRecursive("High", Editorbility.NonEditable)]
	public GameObject m_highVisuals;

	[SerializeField]
	[AssignChildRecursive("Medium", Editorbility.NonEditable)]
	public GameObject m_mediumVisuals;

	[SerializeField]
	[AssignChildRecursive("Low", Editorbility.NonEditable)]
	public GameObject m_lowVisuals;
}
