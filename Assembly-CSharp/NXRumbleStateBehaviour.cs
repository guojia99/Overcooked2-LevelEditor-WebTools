using UnityEngine;

public class NXRumbleStateBehaviour : StateMachineBehaviour
{
	[SerializeField]
	private float m_lowAmplitude = 0.5f;

	[SerializeField]
	private float m_lowFreq = 50f;

	[SerializeField]
	private float m_highAmplitude = 0.5f;

	[SerializeField]
	private float m_highFreq = 60f;

	private NXRumbleManager m_NXRumbleManager;

	private bool m_finished;

	public override void OnStateEnter(Animator _animator, AnimatorStateInfo _stateInfo, int _layerIndex)
	{
	}

	public override void OnStateExit(Animator _animator, AnimatorStateInfo _stateInfo, int _layerIndex)
	{
		m_finished = true;
	}
}
