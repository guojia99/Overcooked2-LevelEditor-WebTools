using UnityEngine;

public class DestroyState : StateMachineBehaviour
{
	public override void OnStateEnter(Animator _animator, AnimatorStateInfo _stateInfo, int _layerIndex)
	{
		_animator.gameObject.Destroy();
	}
}
