using UnityEngine;

public class ClientTerminal : ClientSessionInteractable
{
	private class UserSession : SessionBase
	{
		private PilotMovement m_pilotObject;

		private ClientPilotMovement m_clientPilotObject;

		private Vector3 m_lastPosition;

		private Vector3 m_velocityAverage;

		public Vector2 CosmeticJoystickInput
		{
			get
			{
				if (base.PlayerControls.ControlScheme != null)
				{
					return new Vector2(base.PlayerControls.ControlScheme.m_moveX.GetValue(), 0f - base.PlayerControls.ControlScheme.m_moveY.GetValue());
				}
				m_velocityAverage = m_pilotObject.EstimateAverageVelocity();
				return ClampByMagnitude(m_velocityAverage.XZ() / m_pilotObject.MoveSpeed, 1f);
			}
		}

		public UserSession(ClientTerminal _self, GameObject _avatar, PilotMovement _pilotableObject)
			: base(_self, _avatar)
		{
			m_pilotObject = _pilotableObject;
			m_clientPilotObject = _pilotableObject.gameObject.RequireComponent<ClientPilotMovement>();
			m_clientPilotObject.AssignAvatar(_avatar);
			m_lastPosition = _pilotableObject.transform.position;
		}

		private Vector2 ClampByMagnitude(Vector2 _input, float _maxMagnitude)
		{
			float sqrMagnitude = _input.sqrMagnitude;
			if (sqrMagnitude > _maxMagnitude * _maxMagnitude)
			{
				return _input.normalized * _maxMagnitude;
			}
			return _input;
		}

		public override void OnSessionEnded()
		{
			m_clientPilotObject.AssignAvatar(null);
			base.OnSessionEnded();
		}
	}

	private Terminal m_terminal;

	public Vector2 CosmeticJoystickInput
	{
		get
		{
			if (base.Session != null)
			{
				UserSession userSession = base.Session as UserSession;
				return userSession.CosmeticJoystickInput;
			}
			return Vector2.zero;
		}
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_terminal = (Terminal)synchronisedObject;
	}

	protected override SessionBase BuildSession(GameObject _interacter)
	{
		return new UserSession(this, _interacter, m_terminal.m_pilotableObject);
	}

	public Terminal GetTerminal()
	{
		return m_terminal;
	}
}
