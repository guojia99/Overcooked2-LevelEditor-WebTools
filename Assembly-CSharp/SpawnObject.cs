using UnityEngine;

public class SpawnObject : StateMachineBehaviour
{
	[SerializeField]
	private GameObject m_spanwedObject;

	public override void OnStateEnter(Animator _animator, AnimatorStateInfo _stateInfo, int _layerIndex)
	{
		m_spanwedObject.Instantiate(_animator.transform.position, Quaternion.identity);
	}
}
