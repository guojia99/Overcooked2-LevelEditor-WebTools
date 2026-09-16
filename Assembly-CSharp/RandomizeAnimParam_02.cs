using UnityEngine;

public class RandomizeAnimParam_02 : StateMachineBehaviour
{
	[SerializeField]
	private string m_parameter = string.Empty;

	[SerializeField]
	private float m_minValue;

	[SerializeField]
	private float m_maxValue = 1f;

	private int m_parameterHash;

	protected virtual void Awake()
	{
		m_parameterHash = Animator.StringToHash(m_parameter);
	}

	public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
	{
		base.OnStateEnter(animator, stateInfo, layerIndex);
		animator.SetFloat(m_parameterHash, Random.Range(m_minValue, m_maxValue));
	}
}
