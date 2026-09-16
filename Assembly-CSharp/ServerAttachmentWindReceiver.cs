using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerAttachmentWindReceiver : ServerSynchroniserBase
{
	private AttachmentWindReceiver m_receiver;

	private const float c_MinWindVelocity = 0.05f;

	private ServerPhysicalAttachment m_attachment;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_receiver = (AttachmentWindReceiver)synchronisedObject;
		m_receiver.RegisterVolumeExitedCallback(OnWindVolumeExited);
		m_attachment = base.gameObject.RequestComponent<ServerPhysicalAttachment>();
		m_attachment.RegisterAttachChangedCallback(OnAttachChanged);
	}

	private void FixedUpdate()
	{
		if (m_attachment != null && !m_attachment.IsAttached())
		{
			Vector3 velocity = m_receiver.GetVelocity();
			if (velocity.sqrMagnitude >= 0.05f)
			{
				m_attachment.AccessMotion().Movement(velocity);
			}
		}
	}

	private void OnAttachChanged(IParentable _parentable)
	{
		base.enabled = _parentable == null;
	}

	private void OnWindVolumeExited(IWindSource _source)
	{
		if (m_attachment != null && m_attachment.AccessMotion() != null && m_attachment.AccessMotion().gameObject != null)
		{
			Vector3 velocity = m_attachment.AccessMotion().GetVelocity() + _source.GetVelocity();
			m_attachment.AccessMotion().SetVelocity(velocity);
		}
	}
}
