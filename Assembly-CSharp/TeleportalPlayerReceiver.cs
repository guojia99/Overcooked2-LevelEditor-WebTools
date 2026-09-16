using System;
using UnityEngine;

public class TeleportalPlayerReceiver : BaseTeleportalReceiver
{
	[SerializeField]
	public bool m_groundPlayer = true;

	public ManualAnimation m_ReceiverAnimation;

	private GenericVoid<string> m_animFinishedCallback = delegate
	{
	};

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
