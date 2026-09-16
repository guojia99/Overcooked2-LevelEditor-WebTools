using System.Collections;
using UnityEngine;

public class AnimatedDynamicTransition : DynamicTransitionBase
{
	[SerializeField]
	private Animator m_worldAnimator;

	[SerializeField]
	private string m_worldAnimatorTrigger = string.Empty;

	private int m_worldAnimatorTriggerHash;

	private static readonly int m_iIsTransitioning = Animator.StringToHash("IsTransitioning");

	private CallbackVoid m_endTransitionCallback = delegate
	{
	};

	protected virtual void Awake()
	{
		m_worldAnimatorTriggerHash = Animator.StringToHash(m_worldAnimatorTrigger);
	}

	public override void Setup(CallbackVoid _endTransitionCallback)
	{
		m_endTransitionCallback = _endTransitionCallback;
	}

	public override IEnumerator Run()
	{
		m_worldAnimator.SetTrigger(m_worldAnimatorTriggerHash);
		yield return null;
		while (m_worldAnimator.GetBool(m_iIsTransitioning))
		{
			yield return null;
		}
		m_worldAnimator.ResetTrigger(m_worldAnimatorTriggerHash);
		Shutdown();
	}

	private void Shutdown()
	{
		m_endTransitionCallback();
	}
}
