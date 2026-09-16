using UnityEngine;

public class RandomizeAnimParam : StateMachineBehaviour
{
	[SerializeField]
	private string m_parameter = string.Empty;

	[SerializeField]
	private int m_minValue;

	[SerializeField]
	private int m_maxValue = 1;

	private int m_parameterHash;

	protected virtual void Awake()
	{
		m_parameterHash = Animator.StringToHash(m_parameter);
	}

	public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
	{
		base.OnStateEnter(animator, stateInfo, layerIndex);
		animator.SetInteger(m_parameterHash, Random.Range(m_minValue, m_maxValue + 1));
	}
}
