using UnityEngine;

public class FireAudioEventAfterTime : TriggerAfterTimeState
{
	[Tooltip("Name of the audio trigger to send. Trigger must be a parameter in the Animator")]
	[SerializeField]
	public GameOneShotAudioTag m_audioTag;

	protected override void PerformAction(Animator _animator, AnimatorStateInfo _stateInfo, int _layerIndex)
	{
		_animator.gameObject.SendMessage("AudioTrigger", m_audioTag.ToString());
	}
}
