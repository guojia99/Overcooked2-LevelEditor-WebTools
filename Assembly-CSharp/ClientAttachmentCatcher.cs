using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientAttachmentCatcher : ClientSynchroniserBase, IClientHandleCatch
{
	private GameObject m_trackedThrowable;

	public override EntityType GetEntityType()
	{
		return EntityType.AttachCatcher;
	}

	public override void ApplyServerEvent(Serialisable serialisable)
	{
		AttachmentCatcherMessage attachmentCatcherMessage = (AttachmentCatcherMessage)serialisable;
		m_trackedThrowable = attachmentCatcherMessage.m_object;
	}

	public GameObject GetTrackedThrowable()
	{
		return m_trackedThrowable;
	}
}
