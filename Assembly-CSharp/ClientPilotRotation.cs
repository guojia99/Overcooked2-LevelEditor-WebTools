using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientPilotRotation : ClientPilotMovement
{
	private PilotRotationMessage m_message;

	private PilotRotation m_pilotRotation;

	private Quaternion m_nextRotation;

	public override EntityType GetEntityType()
	{
		return EntityType.PilotRotation;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_pilotRotation = (PilotRotation)synchronisedObject;
		m_nextRotation = m_pilotRotation.m_transformToRotate.rotation;
	}

	public override void UpdateSynchronising()
	{
		m_pilotRotation.m_transformToRotate.rotation = Quaternion.RotateTowards(m_pilotRotation.m_transformToRotate.rotation, m_nextRotation, m_pilotRotation.MoveSpeed * TimeManager.GetDeltaTime(base.gameObject));
	}

	public override void ApplyServerEvent(Serialisable serialisable)
	{
		m_message = (PilotRotationMessage)serialisable;
		Vector3 eulerAngles = m_pilotRotation.m_transformToRotate.eulerAngles;
		eulerAngles.y = m_message.m_angle;
		m_nextRotation = Quaternion.Euler(eulerAngles);
	}

	public override void AssignAvatar(GameObject _avatar)
	{
		base.AssignAvatar(_avatar);
		if (_avatar != null && !string.IsNullOrEmpty(m_pilotRotation.m_sessionBegunTrigger))
		{
			SendMessage("OnTrigger", m_pilotRotation.m_sessionBegunTrigger, SendMessageOptions.DontRequireReceiver);
		}
		else if (_avatar == null && !string.IsNullOrEmpty(m_pilotRotation.m_sessionEndedTrigger))
		{
			SendMessage("OnTrigger", m_pilotRotation.m_sessionEndedTrigger, SendMessageOptions.DontRequireReceiver);
		}
	}
}
