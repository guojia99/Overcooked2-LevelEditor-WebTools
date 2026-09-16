using UnityEngine;

public class ClientPushableObject : ClientSessionInteractable
{
	private class UserSession : SessionBase
	{
		private PushableObject m_PushableObject;

		private ClientPilotMovement m_ClientPilotMovement;

		private bool m_PlayerAttached;

		private bool m_playingAudio;

		private PilotMovement m_PilotMovement;

		private Vector3 m_lastPosition;

		private Vector3 m_velocityAverage;

		public Vector2 CosmeticMovementDirection
		{
			get
			{
				if (base.PlayerControls.ControlScheme != null)
				{
					return new Vector2(base.PlayerControls.ControlScheme.m_moveX.GetValue(), 0f - base.PlayerControls.ControlScheme.m_moveY.GetValue());
				}
				Vector3 position = m_ClientPilotMovement.transform.position;
				float deltaTime = TimeManager.GetDeltaTime(m_ClientPilotMovement.gameObject.layer);
				if (deltaTime > 0.001f)
				{
					Vector3 vector = (position - m_lastPosition) / deltaTime;
					float num = Mathf.Min(10f * deltaTime, 1f);
					m_velocityAverage = num * vector + (1f - num) * m_velocityAverage;
					m_lastPosition = position;
				}
				return ClampByMagnitude(m_velocityAverage.XZ() / m_PilotMovement.MoveSpeed, 1f);
			}
		}

		public UserSession(ClientPushableObject _self, GameObject _avatar, PushableObject _pushableObject)
			: base(_self, _avatar)
		{
			m_PushableObject = _pushableObject;
			m_PilotMovement = _self.gameObject.RequireComponent<PilotMovement>();
			m_ClientPilotMovement = _self.gameObject.RequireComponent<ClientPilotMovement>();
			m_ClientPilotMovement.AssignAvatar(_avatar);
			DynamicLandscapeParenting dynamicLandscapeParenting = base.PlayerControls.gameObject.RequestComponent<DynamicLandscapeParenting>();
			if (dynamicLandscapeParenting != null)
			{
				dynamicLandscapeParenting.enabled = false;
			}
			if (m_PushableObject.m_fakePlayerCollider != null)
			{
				m_PushableObject.m_fakePlayerCollider.transform.position = base.PlayerControls.transform.position;
				m_PushableObject.m_fakePlayerCollider.enabled = true;
			}
			m_lastPosition = m_ClientPilotMovement.transform.position;
		}

		public override void Update()
		{
			base.Update();
			Transform transform = base.PlayerControls.transform;
			if (!m_PlayerAttached && m_PushableObject.IsAttached(transform))
			{
				m_PlayerAttached = true;
				if (m_PushableObject.m_UseAttachPoints)
				{
					transform.localPosition = Vector3.zero;
					transform.localRotation = Quaternion.identity;
				}
				else
				{
					Quaternion rotation = default(Quaternion);
					rotation.SetLookRotation(m_PushableObject.m_CentrePoint.transform.position - transform.position);
					transform.rotation = rotation;
				}
			}
			if (m_PushableObject.m_fakePlayerCollider != null)
			{
				m_PushableObject.m_fakePlayerCollider.transform.position = transform.position;
			}
			if (!m_PushableObject.m_UseAttachPoints && base.PlayerControls.ControlScheme != null)
			{
				Vector3 vector = transform.position - m_PushableObject.m_CentrePoint.transform.position;
				float sqrMagnitude = vector.sqrMagnitude;
				if (Mathf.Abs(sqrMagnitude - m_PushableObject.m_idealPlayerDistance * m_PushableObject.m_idealPlayerDistance) > 0.0001f)
				{
					float num = Mathf.Sqrt(sqrMagnitude);
					if (sqrMagnitude > 0.0001f)
					{
						vector /= num;
					}
					else
					{
						vector = -base.PlayerControls.transform.forward;
						sqrMagnitude = 0.0001f;
					}
					float deltaTime = TimeManager.GetDeltaTime(m_PushableObject.gameObject.layer);
					float num2 = m_PushableObject.m_idealPlayerDistance - num;
					float num3 = ((num2 < 0f) ? (num2 = Mathf.Max(num2, (0f - m_PushableObject.m_lerpPlayerSpeed) * deltaTime)) : (num2 = Mathf.Min(num2, m_PushableObject.m_lerpPlayerSpeed * deltaTime)));
					transform.position += vector * num3;
				}
			}
			ToggleDraggingAudio(CosmeticMovementDirection.sqrMagnitude > 0f);
		}

		private void ToggleDraggingAudio(bool _enabled)
		{
			if (m_PushableObject.m_playDraggingAudio && _enabled != m_playingAudio)
			{
				if (_enabled)
				{
					GameUtils.StartAudio(m_PushableObject.m_draggingAudioTag, this, m_PushableObject.gameObject.layer);
				}
				else
				{
					GameUtils.StopAudio(m_PushableObject.m_draggingAudioTag, this);
				}
				m_playingAudio = _enabled;
			}
		}

		public override void OnSessionEnded()
		{
			m_ClientPilotMovement.AssignAvatar(null);
			base.OnSessionEnded();
			DynamicLandscapeParenting dynamicLandscapeParenting = base.PlayerControls.gameObject.RequestComponent<DynamicLandscapeParenting>();
			if (dynamicLandscapeParenting != null)
			{
				dynamicLandscapeParenting.enabled = true;
			}
			if (m_PushableObject.m_fakePlayerCollider != null)
			{
				m_PushableObject.m_fakePlayerCollider.enabled = false;
			}
			ToggleDraggingAudio(false);
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
	}

	private PushableObject m_PushableObject;

	public Vector2 CosmeticMovementDirection
	{
		get
		{
			if (base.Session != null)
			{
				UserSession userSession = base.Session as UserSession;
				return userSession.CosmeticMovementDirection;
			}
			return Vector2.zero;
		}
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_PushableObject = (PushableObject)synchronisedObject;
		if (m_PushableObject.m_fakePlayerCollider != null)
		{
			m_PushableObject.m_fakePlayerCollider.enabled = false;
		}
	}

	protected override SessionBase BuildSession(GameObject _interacter)
	{
		return new UserSession(this, _interacter, m_PushableObject);
	}
}
