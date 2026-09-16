using UnityEngine;

public class ClearTriggerDuringState : StateMachineBehaviour
{
	[SerializeField]
	private string m_triggerName = string.Empty;

	private int m_triggerNameHash;

	protected virtual void Awake()
	{
		m_triggerNameHash = Animator.StringToHash(m_triggerName);
	}

	public override void OnStateEnter(Animator _animator, AnimatorStateInfo _stateInfo, int _layerIndex)
	{
		_animator.SetBool(m_triggerNameHash, false);
	}

	public override void OnStateExit(Animator _animator, AnimatorStateInfo _stateInfo, int _layerIndex)
	{
		_animator.SetBool(m_triggerNameHash, false);
	}

	public override void OnStateUpdate(Animator _animator, AnimatorStateInfo _stateInfo, int _layerIndex)
	{
		_animator.SetBool(m_triggerNameHash, false);
	}
}
