using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerTriggerOnObject : ServerSynchroniserBase, ITriggerReceiver
{
	private TriggerOnObject m_triggerOnObject;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_triggerOnObject = (TriggerOnObject)synchronisedObject;
	}

	public void OnTrigger(string _trigger)
	{
		if (!(m_triggerOnObject.m_trigger == _trigger))
		{
			return;
		}
		if (m_triggerOnObject.m_targetObject != null)
		{
			m_triggerOnObject.m_targetObject.SendTrigger(m_triggerOnObject.m_triggerToFire);
		}
		for (int i = 0; i < m_triggerOnObject.m_targetObjects.Length; i++)
		{
			if (m_triggerOnObject.m_targetObjects[i] != null)
			{
				m_triggerOnObject.m_targetObjects[i].SendTrigger(m_triggerOnObject.m_triggerToFire);
			}
		}
	}
}
