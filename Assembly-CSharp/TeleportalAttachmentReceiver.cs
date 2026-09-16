using System;
using UnityEngine;

[RequireComponent(typeof(AttachmentThrower))]
public class TeleportalAttachmentReceiver : BaseTeleportalReceiver
{
	private GenericVoid<string> m_animFinishedCallback = delegate
	{
	};

	public ManualAnimation m_ReceiveAnimation;

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
