using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

[RequireComponent(typeof(PlayerAttachmentCarrier))]
public class ServerAttachmentCatcher : ServerSynchroniserBase, IHandleCatch
{
	private AttachmentCatcher m_catcher;

	private AttachmentCatcherMessage m_data = new AttachmentCatcherMessage();

	private GameObject m_trackedThrowable;

	private ServerPlayerAttachmentCarrier m_carrier;

	private PlayerControls m_controls;

	public override EntityType GetEntityType()
	{
		return EntityType.AttachCatcher;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_catcher = (AttachmentCatcher)synchronisedObject;
		m_carrier = base.gameObject.RequireComponent<ServerPlayerAttachmentCarrier>();
		m_controls = base.gameObject.RequireComponent<PlayerControls>();
	}

	private void SendTrackingData(GameObject _tracked)
	{
		m_data.Initialise(_tracked);
		SendServerEvent(m_data);
	}

	public bool CanHandleCatch(ICatchable _object, Vector2 _directionXZ)
	{
		if (!_object.AllowCatch(this, _directionXZ))
		{
			return false;
		}
		GameObject gameObject = _object.AccessGameObject();
		if (m_carrier.InspectCarriedItem() != null)
		{
			return false;
		}
		IThrower thrower = base.gameObject.RequestInterface<IThrower>();
		if (thrower != null)
		{
			IThrowable throwable = gameObject.RequireInterface<IThrowable>();
			if (throwable.GetThrower() == thrower)
			{
				return false;
			}
		}
		float catchDistance = m_catcher.m_catchDistance;
		if ((base.transform.position - gameObject.transform.position).sqrMagnitude > catchDistance * catchDistance)
		{
			return false;
		}
		IAttachment attachment = gameObject.RequireInterface<IAttachment>();
		Vector2 vector = attachment.AccessMotion().GetVelocity().XZ();
		Vector2 vector2 = base.transform.forward.XZ();
		float num = Vector2.Angle(vector, -vector2);
		if (num > m_catcher.m_catchAngleMax)
		{
			return false;
		}
		return true;
	}

	public void HandleCatch(ICatchable _object, Vector2 _directionXZ)
	{
		GameObject gameObject = _object.AccessGameObject();
		m_carrier.CarryItem(gameObject);
		m_trackedThrowable = null;
		SendTrackingData(m_trackedThrowable);
	}

	public void AlertToThrownItem(ICatchable _thrown, IThrower _thrower, Vector2 _directionXZ)
	{
		if (m_controls != null && m_controls.GetDirectlyUnderPlayerControl())
		{
			return;
		}
		if (m_trackedThrowable != null)
		{
			IThrowable throwable = m_trackedThrowable.RequireInterface<IThrowable>();
			if (throwable.IsFlying())
			{
				return;
			}
		}
		if (m_carrier.InspectCarriedItem() == null && m_controls.GetCurrentlyInteracting() == null)
		{
			m_trackedThrowable = _thrown.AccessGameObject();
			SendTrackingData(m_trackedThrowable);
		}
	}

	public int GetCatchingPriority()
	{
		return 0;
	}

	public override void UpdateSynchronising()
	{
		if (m_trackedThrowable != null)
		{
			IThrowable throwable = m_trackedThrowable.RequireInterface<IThrowable>();
			if (!throwable.IsFlying() || m_controls.GetDirectlyUnderPlayerControl())
			{
				m_trackedThrowable = null;
				SendTrackingData(m_trackedThrowable);
			}
		}
	}
}
