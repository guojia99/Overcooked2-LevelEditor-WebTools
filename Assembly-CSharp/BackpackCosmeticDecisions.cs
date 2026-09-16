using UnityEngine;

[RequireComponent(typeof(Backpack))]
public class BackpackCosmeticDecisions : MonoBehaviour
{
	[SerializeField]
	[AssignResource("HoverIconUI", Editorbility.Editable)]
	public GameObject m_contentsHoverIconPrefab;

	[SerializeField]
	public Transform m_hoverIconTarget;

	[SerializeField]
	public Vector3 m_offset = Vector3.zero;
}
