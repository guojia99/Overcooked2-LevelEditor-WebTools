using UnityEngine;

public class ServerPushableObject : ServerSessionInteractable
{
	private class UserSession : SessionBase
	{
		private ServerPilotMovement m_PilotMovement;

		public UserSession(ServerPushableObject _self, GameObject _avatar, PushableObject _pushableObject)
			: base(_self, _avatar)
		{
			m_PilotMovement = _self.gameObject.RequireComponent<ServerPilotMovement>();
			m_PilotMovement.AssignPlayer(base.PlayerControls.ControlScheme);
			DynamicLandscapeParenting dynamicLandscapeParenting = base.PlayerControls.gameObject.RequestComponent<DynamicLandscapeParenting>();
			if (dynamicLandscapeParenting != null)
			{
				dynamicLandscapeParenting.enabled = false;
			}
			Transform transform = base.PlayerControls.gameObject.transform;
			Transform attachPoint = _pushableObject.GetAttachPoint(transform);
			transform.SetParent(attachPoint, true);
		}

		public override void OnSessionEnded()
		{
			m_PilotMovement.AssignPlayer(null);
			base.PlayerControls.gameObject.transform.SetParent(null);
			DynamicLandscapeParenting dynamicLandscapeParenting = base.PlayerControls.gameObject.RequestComponent<DynamicLandscapeParenting>();
			if (dynamicLandscapeParenting != null)
			{
				dynamicLandscapeParenting.enabled = true;
			}
			GroundCast component = base.PlayerControls.GetComponent<GroundCast>();
			if (component != null)
			{
				component.ClearGround();
				component.ForceUpdateNow();
			}
			base.OnSessionEnded();
		}
	}

	private PushableObject m_PushableObject;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_PushableObject = (PushableObject)synchronisedObject;
	}

	protected override SessionBase BuildSession(GameObject _interacter)
	{
		return new UserSession(this, _interacter, m_PushableObject);
	}
}
