using UnityEngine;

public class OvenCosmeticDecisions : MonoBehaviour
{
	[SerializeField]
	[AssignComponentRecursive(Editorbility.NonEditable)]
	public Animator m_animator;
}
