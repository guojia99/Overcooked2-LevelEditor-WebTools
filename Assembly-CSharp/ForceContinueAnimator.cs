using UnityEngine;

[RequireComponent(typeof(Animator))]
public class ForceContinueAnimator : MonoBehaviour
{
	[SerializeField]
	[AssignComponent(Editorbility.NonEditable)]
	private Animator m_animator;

	private void Update()
	{
		m_animator.speed = 1f;
	}
}
