using System.Collections;
using UnityEngine;

public class ServerTeleportalAttachmentSender : ServerBaseTeleportalSender, IHandlePickup, IBaseHandlePickup
{
	private TeleportalAttachmentSender m_attachmentSender;

	private bool m_teleportAnimationFinished;

	private Generic<bool> m_true = () => true;

	protected override void Awake()
	{
		base.Awake();
		m_attachmentSender = base.gameObject.RequireComponent<TeleportalAttachmentSender>();
		m_attachmentSender.RegisterAnimationFinishedCallback(OnAnimationFinished);
	}

	public override bool CanHandleTeleport(ITeleportable _object)
	{
		if (_object is ServerTeleportableItem)
		{
			ServerTeleportableItem serverTeleportableItem = (ServerTeleportableItem)_object;
			if (serverTeleportableItem != null)
			{
				IAttachment attachment = serverTeleportableItem.gameObject.RequestInterface<IAttachment>();
				return attachment != null && (!attachment.IsAttached() || attachment.AccessGameObject().RequestInterfaceUpwardsRecursive<IGridLocation>() != null);
			}
		}
		return false;
	}

	protected override IEnumerator TeleportRoutine(ServerTeleportal _entrancePortal, ITeleportalReceiver _receiver, ITeleportable _object)
	{
		GameObject obj = ((MonoBehaviour)_object).gameObject;
		ServerHandlePickupReferral pickupReferral = obj.RequireComponent<ServerHandlePickupReferral>();
		pickupReferral.SetHandlePickupReferree(this);
		ServerLimitedQuantityItem limitedQuantity = obj.RequestComponent<ServerLimitedQuantityItem>();
		if (null != limitedQuantity)
		{
			limitedQuantity.AddInvincibilityCondition(m_true);
		}
		Vector3 pos = obj.transform.position;
		Quaternion rot = obj.transform.rotation;
		IAttachment attachment = obj.RequestInterface<IAttachment>();
		attachment.Attach(m_attachmentSender);
		obj.transform.SetParent(m_attachmentSender.GetAttachPoint(obj), true);
		obj.transform.SetPositionAndRotation(pos, rot);
		m_teleportAnimationFinished = false;
		while (!m_teleportAnimationFinished)
		{
			yield return null;
		}
		pickupReferral.SetHandlePickupReferree(null);
		attachment.Detach();
		if (null != limitedQuantity)
		{
			limitedQuantity.RemoveInvincibilityCondition(m_true);
			limitedQuantity.Touch();
		}
	}

	public void OnAnimationFinished(string _animName)
	{
		if (_animName == "Teleport")
		{
			m_teleportAnimationFinished = true;
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
