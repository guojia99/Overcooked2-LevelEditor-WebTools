using System.Collections;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientTeleportalAttachmentSender : ClientBaseTeleportalSender, IClientHandlePickup, IBaseHandlePickup
{
	private TeleportalAttachmentSender m_sender;

	private static int m_iTeleport = Animator.StringToHash("Teleport");

	private static int m_iIsFinished = Animator.StringToHash("IsFinished");

	private static int m_iReset = Animator.StringToHash("Reset");

	protected override void Awake()
	{
		base.Awake();
		m_sender = base.gameObject.RequireComponent<TeleportalAttachmentSender>();
	}

	protected override IEnumerator TeleportRoutine(ClientTeleportal _entrancePortal, IClientTeleportalReceiver _receiver, IClientTeleportable _object)
	{
		GameObject obj = ((MonoBehaviour)_object).gameObject;
		Transform attachPoint = m_sender.GetAttachPoint(obj);
		while (obj.transform.parent != attachPoint)
		{
			yield return null;
		}
		ClientWorldObjectSynchroniser synchroniser = obj.RequestComponent<ClientWorldObjectSynchroniser>();
		if (synchroniser != null)
		{
			synchroniser.Pause();
		}
		ClientHandlePickupReferral pickupReferral = obj.RequireComponent<ClientHandlePickupReferral>();
		pickupReferral.SetHandlePickupReferree(this);
		IEnumerator routine = m_sender.m_TeleportAnimation.Run(obj, attachPoint, default(Vector3));
		while (routine.MoveNext())
		{
			yield return null;
		}
		m_sender.OnAnimationFinished("Teleport");
		pickupReferral.SetHandlePickupReferree(null);
		obj.transform.SetParent(null, true);
		obj.SetActive(false);
	}

	public bool CanHandlePickup(ICarrier _carrier)
	{
		return false;
	}

	public int GetPickupPriority()
	{
		return int.MaxValue;
	}
}
