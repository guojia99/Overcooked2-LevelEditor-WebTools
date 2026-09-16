using UnityEngine;

public class IdentNextSceneState : StateMachineBehaviour
{
	public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
	{
		IdentScreenFlow identScreenFlow = Object.FindObjectOfType<IdentScreenFlow>();
		identScreenFlow.ActivateNextScene();
	}
}
