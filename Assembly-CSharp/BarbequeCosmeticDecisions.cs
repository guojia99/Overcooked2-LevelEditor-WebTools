using UnityEngine;

[RequireComponent(typeof(HeatedCookingStation), typeof(AttachStation))]
public class BarbequeCosmeticDecisions : MonoBehaviour
{
	[SerializeField]
	public GameObject m_highEffect;

	[SerializeField]
	public GameObject m_mediumEffect;

	[SerializeField]
	public GameObject m_lowEffect;
}
