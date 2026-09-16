using System;
using UnityEngine;

[RequireComponent(typeof(PhysicalAttachment))]
public class AttachmentWindReceiver : WindAccumulator
{
	private VoidGeneric<IWindSource> m_volumeExitedCallback = delegate
	{
	};

	public override void RemoveWindSource(IWindSource _source)
	{
		base.RemoveWindSource(_source);
		m_volumeExitedCallback(_source);
	}

	public void RegisterVolumeExitedCallback(VoidGeneric<IWindSource> _callback)
	{
		m_volumeExitedCallback = (VoidGeneric<IWindSource>)Delegate.Combine(m_volumeExitedCallback, _callback);
	}

	public void UnregisterVolumeExitedCallback(VoidGeneric<IWindSource> _callback)
	{
		m_volumeExitedCallback = (VoidGeneric<IWindSource>)Delegate.Remove(m_volumeExitedCallback, _callback);
	}
}
