using System;
using UnityEngine;

[RequireComponent(typeof(Teleportal))]
public class TeleportalAttachmentSender : BaseTeleportalSender
{
	private GenericVoid<string> m_animFinishedCallback = delegate
	{
	};

	public ManualAnimation m_TeleportAnimation;

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
