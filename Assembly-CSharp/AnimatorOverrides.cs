using UnityEngine;

[RequireComponent(typeof(Animator))]
public class AnimatorOverrides : MonoBehaviour
{
	[SerializeField]
	private AnimationClipPair[] m_overrides;

	private AnimatorOverrideController overrideController;

	private void Awake()
	{
		Animator animator = base.gameObject.RequireComponent<Animator>();
		overrideController = new AnimatorOverrideController();
		overrideController.runtimeAnimatorController = animator.runtimeAnimatorController;
		overrideController.clips = m_overrides;
		animator.runtimeAnimatorController = overrideController;
	}
}
