using UnityEngine;

public class SendMessageToObject : StateMachineBehaviour
{
	[SerializeField]
	private string m_objectName;

	[SerializeField]
	private string m_message;

	public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
	{
		if (m_objectName != string.Empty)
		{
			GameObject gameObject = GameObject.Find(m_objectName);
			if (gameObject != null)
			{
				gameObject.SendMessage(m_message, SendMessageOptions.DontRequireReceiver);
			}
		}
	}
}
