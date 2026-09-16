using UnityEngine;

public abstract class TriggerAfterTimeState : StateMachineBehaviour
{
	[Tooltip("The time from the start of the animation when the action will be performed")]
	[SerializeField]
	private float m_triggerTime;

	private float m_timer;

	protected abstract void PerformAction(Animator _animator, AnimatorStateInfo _stateInfo, int _layerIndex);

	public override void OnStateEnter(Animator _animator, AnimatorStateInfo _stateInfo, int _layerIndex)
	{
		m_timer = 0f;
		if (m_timer == m_triggerTime)
		{
			PerformAction(_animator, _stateInfo, _layerIndex);
		}
	}

	public override void OnStateUpdate(Animator _animator, AnimatorStateInfo _stateInfo, int _layerIndex)
	{
		if (m_timer < m_triggerTime)
		{
			m_timer += TimeManager.GetDeltaTime(_animator.gameObject) * _animator.speed;
			if (m_timer >= m_triggerTime)
			{
				PerformAction(_animator, _stateInfo, _layerIndex);
			}
		}
	}
}
