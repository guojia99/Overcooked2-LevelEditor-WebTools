using System.Collections;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerTeleportalConveyenceReceiver : ServerSynchroniserBase, IConveyenceReceiver
{
	private TeleportalConveyenceReceiver m_teleportalConveyenceReceiver;

	private ServerTeleportal m_portal;

	private bool m_receiving;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_teleportalConveyenceReceiver = (TeleportalConveyenceReceiver)synchronisedObject;
		m_portal = base.gameObject.RequireComponent<ServerTeleportal>();
	}

	public bool IsReceiving()
	{
		return m_receiving;
	}

	public void InformStartingConveyToMe()
	{
		m_receiving = true;
	}

	public void InformEndingConveyToMe()
	{
		m_receiving = false;
	}

	public IEnumerator ConveyToMe(ServerConveyorStation _priorConveyor, IAttachment _object)
	{
		ITeleportable teleportable = _object.AccessGameObject().RequestInterface<ITeleportable>();
		ServerAttachStation attachStation = _priorConveyor.gameObject.RequireComponent<ServerAttachStation>();
		Transform attachPoint = attachStation.GetAttachPoint(_object.AccessGameObject());
		Transform teleportPoint = m_teleportalConveyenceReceiver.m_attachPoint;
		float progress = 0f;
		float speed = _priorConveyor.GetConveySpeed();
		Vector3 startPos = _object.AccessGameObject().transform.localPosition;
		do
		{
			float dt = TimeManager.GetDeltaTime(base.gameObject);
			progress = Mathf.Clamp01(progress + dt * speed);
			Vector3 endPos = attachPoint.InverseTransformPoint(teleportPoint.position);
			endPos.y = startPos.y;
			_object.AccessGameObject().transform.localPosition = Vector3.Lerp(startPos, endPos, progress);
			yield return null;
		}
		while (progress < 1f);
		_priorConveyor.TakeResponsibilityForItem();
		m_portal.Teleport(teleportable);
	}

	public bool CanConveyTo(IAttachment _itemToConvey)
	{
		if (!m_receiving && _itemToConvey != null)
		{
			ITeleportable teleportable = _itemToConvey.AccessGameObject().RequestInterface<ITeleportable>();
			if (teleportable != null)
			{
				return m_portal.CanTeleport(teleportable);
			}
		}
		return false;
	}

	public void RegisterRefreshedConveyToCallback(CallbackVoid _callback)
	{
	}

	public void UnregisterRefreshedConveyToCallback(CallbackVoid _callback)
	{
	}

	public void RefreshConveyTo()
	{
	}

	public void RegisterAllowConveyToCallback(Generic<bool> _callback)
	{
	}

	public void UnregisterAllowConveyToCallback(Generic<bool> _callback)
	{
	}
}
