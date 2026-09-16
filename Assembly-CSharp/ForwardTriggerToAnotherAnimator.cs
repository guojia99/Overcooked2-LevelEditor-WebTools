using UnityEngine;

public class ForwardTriggerToAnotherAnimator : StateMachineBehaviour
{
	[Tooltip("The name of the trigger on this animator on which to send the trigger to another object")]
	[SerializeField]
	private string m_receiveTrigger = string.Empty;

	[Tooltip("Wehether the received trigger is unset on receiving")]
	[SerializeField]
	private bool m_consumeTrigger = true;

	[Tooltip("The name of the object with the animator in question. Must have an animator")]
	[SerializeField]
	private string m_objectName = string.Empty;

	[Tooltip("Name of the trigger to send. Trigger must be a parameter in the Animator")]
	[SerializeField]
	private string m_sendTriggerName = string.Empty;

	private int m_receiveTriggerHash;

	private int m_sendTriggerNameHash;

	protected virtual void Awake()
	{
		m_receiveTriggerHash = Animator.StringToHash(m_receiveTrigger);
		m_sendTriggerNameHash = Animator.StringToHash(m_sendTriggerName);
	}

	public override void OnStateUpdate(Animator _animator, AnimatorStateInfo _animatorStateInfo, int _layerIndex)
	{
		if (_animator.GetBool(m_receiveTriggerHash))
		{
			if (m_consumeTrigger)
			{
				_animator.ResetTrigger(m_receiveTriggerHash);
			}
			SendTriggerToAnotherAnimator.Send(_animator.gameObject, m_objectName, m_sendTriggerNameHash);
		}
	}
}
