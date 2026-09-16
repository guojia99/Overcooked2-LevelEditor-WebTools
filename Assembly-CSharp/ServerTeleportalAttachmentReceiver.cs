using System.Collections;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerTeleportalAttachmentReceiver : ServerBaseTeleportalReceiver, IHandlePickup, IBaseHandlePickup
{
	private TeleportalAttachmentReceiver m_attachmentReceiver;

	private Generic<bool> m_true = () => true;

	private ServerAttachmentThrower m_thrower;

	private bool m_teleportAnimationFinished;

	private bool m_teleportComplete = true;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_attachmentReceiver = (TeleportalAttachmentReceiver)synchronisedObject;
		m_attachmentReceiver.RegisterAnimationFinishedCallback(OnAnimationFinished);
		m_thrower = base.gameObject.RequireComponent<ServerAttachmentThrower>();
	}

	public override bool CanHandleTeleport(ITeleportable _object)
	{
		return _object is ServerTeleportableItem;
	}

	protected override IEnumerator TeleportRoutine(ServerTeleportal _entrancePortal, ITeleportalSender _sender, ITeleportable _object)
	{
		GameObject obj = ((MonoBehaviour)_object).gameObject;
		ServerHandlePickupReferral pickupReferral = obj.RequireComponent<ServerHandlePickupReferral>();
		pickupReferral.SetHandlePickupReferree(this);
		ServerLimitedQuantityItem limitedQuantity = obj.RequestComponent<ServerLimitedQuantityItem>();
		if (null != limitedQuantity)
		{
			limitedQuantity.AddInvincibilityCondition(m_true);
		}
		IAttachment attachment = obj.RequestInterface<IAttachment>();
		attachment.Attach(m_attachmentReceiver);
		m_teleportAnimationFinished = false;
		m_teleportComplete = false;
		while (!m_teleportAnimationFinished)
		{
			yield return null;
		}
		m_teleportAnimationFinished = false;
		attachment.Detach();
		pickupReferral.SetHandlePickupReferree(null);
		GameObject root = ((MonoBehaviour)_object).gameObject;
		GroundCast groundCast = root.RequestComponent<GroundCast>();
		if (groundCast != null)
		{
			groundCast.ForceUpdateNow();
		}
		ServerWorldObjectSynchroniser synchroniser = root.RequestComponent<ServerWorldObjectSynchroniser>();
		if (synchroniser != null)
		{
			synchroniser.ResumeAllClients();
		}
		if (null != limitedQuantity)
		{
			limitedQuantity.RemoveInvincibilityCondition(m_true);
			limitedQuantity.Touch();
		}
		while (!m_teleportComplete)
		{
			yield return null;
		}
		IThrowable throwable = obj.RequestInterface<IThrowable>();
		if (throwable != null)
		{
			Vector2 normalized = m_attachmentReceiver.GetAttachPoint(root).forward.XZ().normalized;
			if (throwable.CanHandleThrow(m_thrower, normalized))
			{
				throwable.HandleThrow(m_thrower, normalized);
			}
		}
	}

	public void OnAnimationFinished(string _animName)
	{
		if (_animName == "Receive")
		{
			m_teleportAnimationFinished = true;
		}
		if (_animName == "TeleportComplete")
		{
			m_teleportComplete = true;
		}
	}

	public bool CanHandlePickup(ICarrier _carrier)
	{
		return false;
	}

	public void HandlePickup(ICarrier _carrier, Vector2 _directionXZ)
	{
	}

	public int GetPickupPriority()
	{
		return int.MaxValue;
	}
}
