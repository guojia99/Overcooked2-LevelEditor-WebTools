using System.Collections;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientTeleportalAttachmentReceiver : ClientBaseTeleportalReceiver, IClientHandlePickup, IBaseHandlePickup
{
	private TeleportalAttachmentReceiver m_receiver;

	private static int m_iIsFinished = Animator.StringToHash("IsFinished");

	private static int m_iReceive = Animator.StringToHash("Receive");

	private static int m_iReset = Animator.StringToHash("Reset");

	protected override void Awake()
	{
		base.Awake();
		m_receiver = base.gameObject.RequireComponent<TeleportalAttachmentReceiver>();
	}

	protected override IEnumerator TeleportRoutine(ClientTeleportal _entrancePortal, IClientTeleportalSender _sender, IClientTeleportable _object)
	{
		GameObject obj = ((MonoBehaviour)_object).gameObject;
		ClientHandlePickupReferral pickupReferral = obj.RequireComponent<ClientHandlePickupReferral>();
		pickupReferral.SetHandlePickupReferree(this);
		Transform attachPoint = m_receiver.GetAttachPoint(obj);
		Transform prevParent = obj.transform.parent;
		obj.transform.SetParent(attachPoint, true);
		obj.transform.localPosition = Vector3.zero;
		obj.transform.localRotation = Quaternion.identity;
		obj.SetActive(true);
		IEnumerator routine = m_receiver.m_ReceiveAnimation.Run(obj, attachPoint, default(Vector3));
		while (routine.MoveNext())
		{
			yield return null;
		}
		m_receiver.OnAnimationFinished("Receive");
		ClientWorldObjectSynchroniser synchroniser = obj.RequestComponent<ClientWorldObjectSynchroniser>();
		if (synchroniser != null)
		{
			while (!synchroniser.IsReadyToResume())
			{
				yield return null;
			}
			synchroniser.Resume();
		}
		pickupReferral.SetHandlePickupReferree(null);
		obj.transform.localScale = Vector3.one;
		while (obj.transform.parent == m_receiver.GetAttachPoint(obj))
		{
			yield return null;
		}
		m_receiver.OnAnimationFinished("TeleportComplete");
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
