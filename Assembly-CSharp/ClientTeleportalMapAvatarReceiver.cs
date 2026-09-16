using System.Collections;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientTeleportalMapAvatarReceiver : ClientBaseTeleportalReceiver
{
	private TeleportalMapAvatarReceiver m_receiver;

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
		m_receiver = base.gameObject.RequireComponent<TeleportalMapAvatarReceiver>();
	}

	protected override IEnumerator TeleportRoutine(ClientTeleportal _entrancePortal, IClientTeleportalSender _sender, IClientTeleportable _object)
	{
		GameObject root = ((MonoBehaviour)_object).gameObject;
		MapAvatarControls controls = root.RequestComponentUpwardsRecursive<MapAvatarControls>();
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
		ClientTeleportalMapAvatarSender clientMapAvatarSender = _sender as ClientTeleportalMapAvatarSender;
		if (clientMapAvatarSender != null)
		{
			TeleportalMapAvatarSender mapAvatarSender = clientMapAvatarSender.gameObject.RequestComponent<TeleportalMapAvatarSender>();
			if (mapAvatarSender != null)
			{
				if (mapAvatarSender.TransitionType == TeleportalMapAvatarSender.TransitionTypes.ScreenTransition)
				{
					ScreenTransitionManager screenTransitionManager = GameUtils.RequireManager<ScreenTransitionManager>();
					MoveCameraToTarget(root.transform);
					bool transitionDone = false;
					screenTransitionManager.StartTransitionDown(delegate
					{
						transitionDone = true;
					});
					while (!transitionDone)
					{
						yield return null;
					}
				}
				if (mapAvatarSender.m_useMotionBlur && mapAvatarSender.m_postProcessingBehaviour != null)
				{
					mapAvatarSender.m_postProcessingBehaviour.profile.motionBlur.enabled = false;
				}
			}
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
		MapAvatarDynamicLandscapeParenting dynamicParenting = root.RequestComponent<MapAvatarDynamicLandscapeParenting>();
		if (dynamicParenting != null)
		{
			dynamicParenting.enabled = true;
		}
		MapAvatarGroundCast groundCast = root.RequestComponent<MapAvatarGroundCast>();
		if (groundCast != null)
		{
			groundCast.enabled = true;
		}
		collider.enabled = true;
		m_receiver.OnAnimationFinished("Receive");
		ClientWorldObjectSynchroniser synchroniser = root.RequestComponent<ClientWorldObjectSynchroniser>();
		if (synchroniser != null)
		{
			while (!synchroniser.IsReadyToResume())
			{
				yield return null;
			}
			synchroniser.Resume();
		}
		Rigidbody rigidbody = root.RequireComponent<Rigidbody>();
		rigidbody.isKinematic = false;
		controls.enabled = true;
		ParticleSystem pfx = root.RequestComponentRecursive<ParticleSystem>();
		if (pfx != null)
		{
			pfx.Play();
		}
	}

	private void MoveCameraToTarget(Transform _target)
	{
		Camera main = Camera.main;
		WorldMapCamera worldMapCamera = main.gameObject.RequireComponent<WorldMapCamera>();
		Vector3 accessIdealOffset = worldMapCamera.AccessIdealOffset;
		worldMapCamera.transform.position = _target.position + accessIdealOffset;
	}
}
