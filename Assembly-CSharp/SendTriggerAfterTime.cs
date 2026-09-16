using UnityEngine;

public class SendTriggerAfterTime : StateMachineBehaviourEx
{
	[SerializeField]
	private string m_animatorName = string.Empty;

	[Tooltip("Name of the trigger to send. Trigger must be a parameter in the Animator")]
	public string TriggerName = string.Empty;

	[Tooltip("The time from the start of the aniamtion when the trigger will be sent")]
	public float TriggerTime = 1f;

	private float m_timer;

	[SerializeField]
	private bool OrTriggerOnExit;

	private int m_iTriggerName;

	protected virtual void Awake()
	{
		m_iTriggerName = Animator.StringToHash(TriggerName);
	}

	public override void OnStateEnter(Animator _animator, AnimatorStateInfo _stateInfo, int _layerIndex)
	{
		m_timer = 0f;
		if (!OrTriggerOnExit && TriggerTime == 0f)
		{
			GetAnimator(_animator).SetTrigger(m_iTriggerName);
		}
	}

	public override void OnStateUpdate(Animator _animator, AnimatorStateInfo _stateInfo, int _layerIndex)
	{
		if (!OrTriggerOnExit && m_timer < TriggerTime)
		{
			m_timer += TimeManager.GetDeltaTime(_animator.gameObject) * _animator.speed;
			if (m_timer >= TriggerTime)
			{
				GetAnimator(_animator).SetTrigger(m_iTriggerName);
			}
		}
	}

	public override void OnStateExit(Animator _animator, AnimatorStateInfo _stateInfo, int _layerIndex)
	{
		if (OrTriggerOnExit && m_timer < TriggerTime)
		{
			GetAnimator(_animator).SetTrigger(m_iTriggerName);
		}
	}

	private Animator GetAnimator(Animator _thisAnimator)
	{
		if (m_animatorName != string.Empty)
		{
			return GameObject.Find(m_animatorName).RequireComponent<Animator>();
		}
		return _thisAnimator;
	}
}
