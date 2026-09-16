using System.Collections;
using UnityEngine;

public class CoroutineStateBehaviour : StateMachineBehaviourEx
{
	private IEnumerator m_runCo;

	protected virtual void OnEnter(Animator _animator, AnimatorStateInfo _stateInfo, int _layerIndex)
	{
	}

	protected virtual void OnUpdate(Animator _animator, AnimatorStateInfo _animatorStateInfo, int _layerIndex)
	{
	}

	protected virtual void OnExit(Animator _animator, AnimatorStateInfo _stateInfo, int _layerIndex)
	{
	}

	protected virtual IEnumerator Run(Animator _animator, AnimatorStateInfo _stateInfo, int _layerIndex)
	{
		yield return null;
	}

	protected virtual void OnRunComplete()
	{
	}

	public override void OnStateEnter(Animator _animator, AnimatorStateInfo _stateInfo, int _layerIndex)
	{
		OnEnter(_animator, _stateInfo, _layerIndex);
		m_runCo = Run(_animator, _stateInfo, _layerIndex);
	}

	public override void OnStateUpdate(Animator _animator, AnimatorStateInfo _animatorStateInfo, int _layerIndex)
	{
		if (m_runCo != null && !m_runCo.MoveNext())
		{
			m_runCo = null;
			OnRunComplete();
		}
		OnUpdate(_animator, _animatorStateInfo, _layerIndex);
	}

	public override void OnStateExit(Animator _animator, AnimatorStateInfo _stateInfo, int _layerIndex)
	{
		OnExit(_animator, _stateInfo, _layerIndex);
	}
}
