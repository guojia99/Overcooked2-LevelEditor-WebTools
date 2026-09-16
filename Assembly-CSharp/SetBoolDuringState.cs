using UnityEngine;

public class SetBoolDuringState : StateMachineBehaviour
{
	[SerializeField]
	private string m_variableName;

	[SerializeField]
	private string m_animatorName = string.Empty;

	[SerializeField]
	private bool m_invert;

	private int m_variableNameHash;

	private Animator m_animator;

	protected virtual void Awake()
	{
		m_variableNameHash = Animator.StringToHash(m_variableName);
	}

	public override void OnStateEnter(Animator _animator, AnimatorStateInfo _stateInfo, int _layerIndex)
	{
		m_animator = _animator;
		if (m_animatorName != string.Empty)
		{
			GameObject obj = GameObject.Find(m_animatorName);
			m_animator = obj.RequireComponent<Animator>();
		}
		if (m_variableNameHash != 0 && m_animator != null)
		{
			m_animator.SetBool(m_variableNameHash, !m_invert);
		}
	}

	public override void OnStateExit(Animator _animator, AnimatorStateInfo _stateInfo, int _layerIndex)
	{
		if (m_variableNameHash != 0 && m_animator != null)
		{
			m_animator.SetBool(m_variableNameHash, m_invert);
		}
	}
}
