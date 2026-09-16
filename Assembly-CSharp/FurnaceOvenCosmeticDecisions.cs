using UnityEngine;

public class FurnaceOvenCosmeticDecisions : MonoBehaviour
{
	[SerializeField]
	[AssignComponentRecursive(Editorbility.NonEditable)]
	public Animator m_animator;

	[SerializeField]
	public GameObject m_highEffect;

	[SerializeField]
	public GameObject m_mediumEffect;

	[SerializeField]
	public GameObject m_lowEffect;
}
