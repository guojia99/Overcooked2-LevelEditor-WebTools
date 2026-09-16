using UnityEngine;

public class SendTriggerToAnotherAnimator : TriggerAfterTimeState
{
	[Tooltip("The name of the object with the animator in question. Must have an animator")]
	[SerializeField]
	private string m_objectName = string.Empty;

	[Tooltip("Name of the trigger to send. Trigger must be a parameter in the Animator")]
	[SerializeField]
	private string m_triggerName = string.Empty;

	private int m_triggerNameHash;

	protected virtual void Awake()
	{
		m_triggerNameHash = Animator.StringToHash(m_triggerName);
	}

	protected override void PerformAction(Animator _animator, AnimatorStateInfo _stateInfo, int _layerIndex)
	{
		Send(_animator.gameObject, m_objectName, m_triggerNameHash);
	}

	public static void Send(GameObject _sender, string _objectName, string _trigger)
	{
		GameObject obj = GameObject.Find(_objectName);
		Animator animator = obj.RequestComponentRecursive<Animator>();
		animator.SetTrigger(_trigger);
	}

	public static void Send(GameObject _sender, string _objectName, int _trigger)
	{
		GameObject obj = GameObject.Find(_objectName);
		Animator animator = obj.RequestComponentRecursive<Animator>();
		animator.SetTrigger(_trigger);
	}
}
