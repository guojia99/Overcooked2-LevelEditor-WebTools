using System.Collections;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientTeleportalPlayerReceiver : ClientBaseTeleportalReceiver
{
	private TeleportalPlayerReceiver m_receiver;

	private const float c_maxGroundCastDistance = 2f;

	private static LayerMask c_groundCastMask = 0;

	private Generic<bool> m_canTeleportCallback = () => true;

	private ClientTeleportCallback m_teleportStartedCallback = delegate
	{
	};

	private ClientTeleportCallback m_teleportFinishedCallback = delegate
	{
	};

	private static readonly int m_iReceive = Animator.StringToHash("Receive");

	private static readonly int m_iReset = Animator.StringToHash("Reset");

	private static readonly int m_iIsFinished = Animator.StringToHash("IsFinished");

	protected override void Awake()
	{
		base.Awake();
		if ((int)c_groundCastMask == 0)
		{
			c_groundCastMask = LayerMask.GetMask("Ground", "SlopedGround");
		}
		m_receiver = base.gameObject.RequireComponent<TeleportalPlayerReceiver>();
	}

	protected override IEnumerator TeleportRoutine(ClientTeleportal _entrancePortal, IClientTeleportalSender _sender, IClientTeleportable _object)
	{
		GameObject root = ((MonoBehaviour)_object).gameObject;
		PlayerControls controls = root.RequestComponentUpwardsRecursive<PlayerControls>();
		root = controls.gameObject;
		Transform attachPoint = m_receiver.GetAttachPoint(root);
		root.transform.SetParent(attachPoint);
		root.transform.rotation = Quaternion.identity;
		root.transform.localScale = Vector3.one;
		Collider collider = root.RequestComponent<Collider>();
		Vector3 offset = default(Vector3);
		RaycastHit raycastInfo;
		if (m_receiver.m_groundPlayer && Physics.Raycast(new Ray(attachPoint.position, -Vector3.up), out raycastInfo, 2f, c_groundCastMask))
		{
			offset = raycastInfo.point - attachPoint.position;
			root.transform.SetPositionAndRotation(raycastInfo.point, attachPoint.rotation);
		}
		else
		{
			Vector3 position = root.transform.position + (attachPoint.position - collider.bounds.center);
			root.transform.SetPositionAndRotation(position, attachPoint.rotation);
			offset = collider.bounds.extents;
		}
		Animator mesh = root.RequestComponentRecursive<Animator>();
		if (mesh != null)
		{
			mesh.gameObject.SetActive(true);
		}
		IEnumerator routine = m_receiver.m_ReceiverAnimation.Run(root, attachPoint, offset);
		while (routine.MoveNext())
		{
			yield return null;
		}
		root.transform.SetParent(null, true);
		DynamicLandscapeParenting dynamicParenting = root.RequestComponent<DynamicLandscapeParenting>();
		if (dynamicParenting != null)
		{
			dynamicParenting.enabled = true;
		}
		collider.enabled = true;
		m_receiver.OnAnimationFinished("Receive");
		ClientWorldObjectSynchroniser synchroniser = root.RequestComponent<ClientWorldObjectSynchroniser>();
		if (synchroniser != null)
		{
			yield return null;
			while (!synchroniser.IsReadyToResume())
			{
				yield return null;
			}
			synchroniser.Resume();
		}
		controls.enabled = true;
		controls.Motion.SetKinematic(false);
		ParticleSystem pfx = root.RequestComponentRecursive<ParticleSystem>();
		if (pfx != null)
		{
			pfx.Play();
		}
	}
}
