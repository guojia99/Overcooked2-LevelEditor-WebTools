using System;
using UnityEngine;
using UnityEngine.PostProcessing;

[RequireComponent(typeof(Teleportal))]
public class TeleportalMapAvatarSender : BaseTeleportalSender
{
	[Serializable]
	public enum TransitionTypes
	{
		None = 0,
		ScreenTransition = 1,
		Lerp = 2,
		Instant = 3
	}

	[Serializable]
	public class LerpConfig
	{
		[SerializeField]
		public float GradientLimit = 20f;

		[SerializeField]
		public float TimeToMax = 0.5f;
	}

	[SerializeField]
	private TransitionTypes m_transitionType = TransitionTypes.Lerp;

	[SerializeField]
	[HideInInspectorTest("m_transitionType", TransitionTypes.Lerp)]
	public LerpConfig m_lerpConfig = new LerpConfig();

	[SerializeField]
	public bool m_useMotionBlur = true;

	[SerializeField]
	[HideInInspectorTest("m_useMotionBlur", true)]
	public PostProcessingBehaviour m_postProcessingBehaviour;

	private GenericVoid<string> m_animFinishedCallback = delegate
	{
	};

	public ManualAnimation m_SenderAnimation;

	public TransitionTypes TransitionType
	{
		get
		{
			return m_transitionType;
		}
	}

	private void Awake()
	{
		if (m_useMotionBlur && m_postProcessingBehaviour != null)
		{
			m_postProcessingBehaviour.profile.motionBlur.enabled = false;
		}
	}

	public void RegisterAnimationFinishedCallback(GenericVoid<string> _callback)
	{
		m_animFinishedCallback = (GenericVoid<string>)Delegate.Combine(m_animFinishedCallback, _callback);
	}

	public void DeregisterAnimationFinishedCallback(GenericVoid<string> _callback)
	{
		m_animFinishedCallback = (GenericVoid<string>)Delegate.Remove(m_animFinishedCallback, _callback);
	}

	public void OnAnimationFinished(string _animName)
	{
		m_animFinishedCallback(_animName);
	}
}
