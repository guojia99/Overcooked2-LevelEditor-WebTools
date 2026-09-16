using UnityEngine;

public class TriggerAfterRandomTime : StateMachineBehaviour
{
	[SerializeField]
	private string m_animatorName = string.Empty;

	[Tooltip("Name of the trigger to send. Trigger must be a parameter in the Animator")]
	public string TriggerName = string.Empty;

	public float MinValue;

	public float MaxValue = 1f;

	private float m_timer;

	private int m_TriggerNameHash;

	protected virtual void Awake()
	{
		m_TriggerNameHash = Animator.StringToHash(TriggerName);
	}

	public override void OnStateEnter(Animator _animator, AnimatorStateInfo _stateInfo, int _layerIndex)
	{
		m_timer = Random.Range(MinValue, MaxValue);
		if (m_timer <= 0f)
		{
			GetAnimator(_animator).SetTrigger(m_TriggerNameHash);
		}
	}

	public override void OnStateUpdate(Animator _animator, AnimatorStateInfo _stateInfo, int _layerIndex)
	{
		if (m_timer > 0f)
		{
			m_timer -= TimeManager.GetDeltaTime(_animator.gameObject) * _animator.speed;
			if (m_timer <= 0f)
			{
				GetAnimator(_animator).SetTrigger(m_TriggerNameHash);
			}
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
