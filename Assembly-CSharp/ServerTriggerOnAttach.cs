using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerTriggerOnAttach : ServerSynchroniserBase
{
	private TriggerOnAttach m_triggerOnAttach;

	private ServerAttachStation m_attachStation;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_triggerOnAttach = (TriggerOnAttach)synchronisedObject;
		m_attachStation = m_triggerOnAttach.gameObject.RequireComponent<ServerAttachStation>();
		m_attachStation.RegisterOnItemAdded(OnItemAdded);
		m_attachStation.RegisterOnItemRemoved(OnItemRemoved);
	}

	public void OnItemAdded(IAttachment _iHoldable)
	{
		if (m_triggerOnAttach.m_triggerTarget != null && !string.IsNullOrEmpty(m_triggerOnAttach.m_attachTrigger))
		{
			m_triggerOnAttach.m_triggerTarget.SendTrigger(m_triggerOnAttach.m_attachTrigger);
		}
	}

	public void OnItemRemoved(IAttachment _iHoldable)
	{
		if (m_triggerOnAttach.m_triggerTarget != null && !string.IsNullOrEmpty(m_triggerOnAttach.m_detachTrigger))
		{
			m_triggerOnAttach.m_triggerTarget.SendTrigger(m_triggerOnAttach.m_detachTrigger);
		}
	}
}
