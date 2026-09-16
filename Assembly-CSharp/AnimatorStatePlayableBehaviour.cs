using UnityEngine;
using UnityEngine.Playables;

public class AnimatorStatePlayableBehaviour : PlayableBehaviour
{
	private AnimatorVariableType m_variableType;

	private int m_variableNameHash;

	private object m_variableValue;

	private Animator m_targetAnimator;

	private bool m_pending;

	private object m_originalValue;

	public void Setup(string _variableName, AnimatorVariableType _variableType, object _value)
	{
		m_variableNameHash = Animator.StringToHash(_variableName);
		m_variableType = _variableType;
		m_variableValue = _value;
	}

	public override void OnBehaviourPlay(Playable playable, FrameData info)
	{
		base.OnBehaviourPlay(playable, info);
		if (Application.isPlaying && info.evaluationType == FrameData.EvaluationType.Playback)
		{
			m_pending = true;
		}
	}

	public override void OnBehaviourPause(Playable playable, FrameData info)
	{
		base.OnBehaviourPause(playable, info);
		if (Application.isPlaying && info.evaluationType == FrameData.EvaluationType.Playback && m_targetAnimator != null && m_originalValue != null)
		{
			AnimatorUtils.SetValue(m_targetAnimator, m_variableNameHash, m_variableType, m_originalValue);
		}
		m_pending = false;
	}

	public override void ProcessFrame(Playable playable, FrameData info, object playerData)
	{
		base.ProcessFrame(playable, info, playerData);
		if (!Application.isPlaying || info.evaluationType != FrameData.EvaluationType.Playback)
		{
			return;
		}
		if (m_targetAnimator == null)
		{
			m_targetAnimator = playerData as Animator;
		}
		if (m_pending && m_targetAnimator != null)
		{
			m_originalValue = AnimatorUtils.GetValue(m_targetAnimator, m_variableNameHash, m_variableType);
			AnimatorUtils.SetValue(m_targetAnimator, m_variableNameHash, m_variableType, m_variableValue);
			if (!info.seekOccurred)
			{
			}
		}
	}
}
