using UnityEngine;

[ExecutionDependency(typeof(TriggerAnimatorSetVariable))]
[RequireComponent(typeof(Animator))]
public class AnimatorCommunications : MonoBehaviour
{
	private Animator m_animator;

	private bool m_animatorEnabled;

	public CallbackVoid AnimatorEnabledCallback = delegate
	{
	};

	public CallbackVoid AnimatorDisabledCallback = delegate
	{
	};

	private void Awake()
	{
		m_animator = base.gameObject.RequireComponent<Animator>();
		m_animatorEnabled = m_animator.enabled;
	}

	public void FireAnimatorTrigger(string _trigger)
	{
		m_animator.SetTrigger(_trigger);
	}

	private void OnEnable()
	{
		LateUpdate();
	}

	private void LateUpdate()
	{
		if (m_animator != null && m_animatorEnabled != m_animator.enabled)
		{
			m_animatorEnabled = m_animator.enabled;
			if (m_animatorEnabled)
			{
				AnimatorEnabledCallback();
			}
			else
			{
				AnimatorDisabledCallback();
			}
		}
	}

	private void OnDisable()
	{
		LateUpdate();
	}
}
