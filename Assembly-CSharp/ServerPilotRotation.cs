using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerPilotRotation : ServerPilotMovement
{
	private PilotRotationMessage m_message = new PilotRotationMessage();

	private PilotRotation m_pilotRotation;

	private Vector3 m_startRightDirection;

	private float m_startAngle;

	private float m_angle;

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_pilotRotation = (PilotRotation)synchronisedObject;
		m_startRightDirection = m_pilotRotation.m_transformToRotate.right;
		m_startAngle = m_pilotRotation.m_transformToRotate.eulerAngles.y;
	}

	public override void UpdateSynchronising()
	{
		if (m_controlScheme != null)
		{
			UpdateRotation();
		}
	}

	public override EntityType GetEntityType()
	{
		return EntityType.PilotRotation;
	}

	private void UpdateRotation()
	{
		float value = m_controlScheme.m_moveX.GetValue();
		float num = 0f - m_controlScheme.m_moveY.GetValue();
		float num2 = 0.1f;
		if (value != 0f || num != 0f)
		{
			float num3 = Vector3.Dot(rhs: new Vector3(value, 0f, num), lhs: m_startRightDirection);
			if (Mathf.Abs(num3) > num2)
			{
				m_angle += m_pilotRotation.MoveSpeed * (float)((!(num3 < 0f)) ? 1 : (-1)) * TimeManager.GetDeltaTime(base.gameObject);
				m_angle = Mathf.Clamp(m_angle, m_pilotRotation.m_minLimitDegrees, m_pilotRotation.m_maxLimitDegrees);
				m_message.m_angle = m_startAngle + m_angle;
				SendServerEvent(m_message);
			}
		}
	}
}
