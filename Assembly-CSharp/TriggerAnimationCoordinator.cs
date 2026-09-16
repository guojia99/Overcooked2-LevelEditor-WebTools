using UnityEngine;

public class TriggerAnimationCoordinator : MonoBehaviour
{
	[SerializeField]
	[AssignResource("BaseGenericAnimator", Editorbility.NonEditable)]
	public RuntimeAnimatorController m_templateAnimator;

	[SerializeField]
	[AssignResource("Blank", Editorbility.NonEditable)]
	public AnimationClip m_templateClip;

	[SerializeField]
	[ReadOnly]
	public string m_animationStartTrigger = "Animate";

	[SerializeField]
	[ReadOnly]
	public string m_animationFinishedTrigger = "AnimationFinished";

	[SerializeField]
	[ReadOnly]
	public string m_animationReadyState = "Idle";

	[SerializeField]
	public bool m_triggerOnAnimator = true;

	public int m_iAnimationStartTriggerHash;

	public int m_iAnimationFinishedTriggerHash;

	public int m_iAnimationReadyStateHash;

	protected virtual void Awake()
	{
		m_iAnimationStartTriggerHash = Animator.StringToHash(m_animationStartTrigger);
		m_iAnimationFinishedTriggerHash = Animator.StringToHash(m_animationFinishedTrigger);
		m_iAnimationReadyStateHash = Animator.StringToHash(m_animationReadyState);
	}
}
