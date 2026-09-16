using UnityEngine;

public class StateMachineBehaviourEx : StateMachineBehaviour
{
	[SelfAssignID(Visibility.Show)]
	public int SharedBehaviour;

	public StateMachineBehaviourEx GetInstance(Animator _animator)
	{
		StateMachineBehaviourEx[] behaviours = _animator.GetBehaviours<StateMachineBehaviourEx>();
		for (int i = 0; i < behaviours.Length; i++)
		{
			if (behaviours[i].SharedBehaviour == SharedBehaviour)
			{
				return behaviours[i];
			}
		}
		return null;
	}
}
