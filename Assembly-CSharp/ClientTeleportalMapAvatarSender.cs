using System.Collections;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientTeleportalMapAvatarSender : ClientBaseTeleportalSender
{
	private TeleportalMapAvatarSender m_sender;

	private static readonly int m_iTeleport = Animator.StringToHash("Teleport");

	private static readonly int m_iIsFinished = Animator.StringToHash("IsFinished");

	private static readonly int m_iReset = Animator.StringToHash("Reset");

	protected override void Awake()
	{
		base.Awake();
		m_sender = base.gameObject.RequireComponent<TeleportalMapAvatarSender>();
	}

	protected override IEnumerator TeleportRoutine(ClientTeleportal _entrancePortal, IClientTeleportalReceiver _receiver, IClientTeleportable _object)
	{
		GameObject root = ((MonoBehaviour)_object).gameObject;
		MapAvatarControls controls = root.RequireComponent<MapAvatarControls>();
		root = controls.gameObject;
		controls.enabled = false;
		Rigidbody rigidbody = root.RequireComponent<Rigidbody>();
		rigidbody.isKinematic = true;
		rigidbody.velocity = Vector3.zero;
		Collider collider = root.RequireComponent<Collider>();
		collider.enabled = false;
		MapAvatarGroundCast groundCast = root.RequestComponent<MapAvatarGroundCast>();
		if (groundCast != null)
		{
			groundCast.enabled = false;
		}
		MapAvatarDynamicLandscapeParenting dynamicParenting = root.RequestComponent<MapAvatarDynamicLandscapeParenting>();
		if (dynamicParenting != null)
		{
			dynamicParenting.enabled = false;
		}
		ClientWorldObjectSynchroniser synchroniser = root.RequestComponent<ClientWorldObjectSynchroniser>();
		if (synchroniser != null)
		{
			synchroniser.Pause();
		}
		Transform attachPoint = m_sender.GetAttachPoint(root);
		root.transform.SetParent(attachPoint, true);
		IEnumerator routine = m_sender.m_SenderAnimation.Run(root, attachPoint, default(Vector3));
		while (routine.MoveNext())
		{
			yield return null;
		}
		m_sender.OnAnimationFinished("Teleport");
		root.transform.SetParent(null, true);
		root.transform.rotation = Quaternion.identity;
		if (m_sender.m_useMotionBlur && m_sender.m_postProcessingBehaviour != null)
		{
			m_sender.m_postProcessingBehaviour.profile.motionBlur.enabled = true;
		}
		Animator mesh = root.RequestComponentRecursive<Animator>();
		if (mesh != null)
		{
			mesh.gameObject.SetActive(true);
		}
		ParticleSystem pfx = root.RequestComponentRecursive<ParticleSystem>();
		if (pfx != null)
		{
			pfx.Stop(true, ParticleSystemStopBehavior.StopEmitting);
		}
		IEnumerator transitionRoutine = null;
		switch (m_sender.TransitionType)
		{
		case TeleportalMapAvatarSender.TransitionTypes.ScreenTransition:
			transitionRoutine = ScreenTransitionRoutine();
			break;
		case TeleportalMapAvatarSender.TransitionTypes.Lerp:
		{
			MonoBehaviour monoBehaviour2 = _receiver as MonoBehaviour;
			if (monoBehaviour2 != null)
			{
				transitionRoutine = CameraLerpRoutine(monoBehaviour2.transform.position);
			}
			break;
		}
		case TeleportalMapAvatarSender.TransitionTypes.Instant:
		{
			MonoBehaviour monoBehaviour = _receiver as MonoBehaviour;
			if (monoBehaviour != null)
			{
				Camera main = Camera.main;
				WorldMapCamera worldMapCamera = main.gameObject.RequireComponent<WorldMapCamera>();
				Vector3 accessIdealOffset = worldMapCamera.AccessIdealOffset;
				main.transform.position = monoBehaviour.transform.position + accessIdealOffset;
			}
			break;
		}
		}
		while (transitionRoutine != null && transitionRoutine.MoveNext())
		{
			yield return null;
		}
	}

	private IEnumerator ScreenTransitionRoutine()
	{
		ScreenTransitionManager screenTransitionManager = GameUtils.RequireManager<ScreenTransitionManager>();
		bool transitionDone = false;
		screenTransitionManager.StartTransitionUp(delegate
		{
			transitionDone = true;
		});
		while (!transitionDone)
		{
			yield return null;
		}
	}

	private IEnumerator CameraLerpRoutine(Vector3 _endPos)
	{
		Camera mainCamera = Camera.main;
		WorldMapCamera worldMapCamera = mainCamera.gameObject.RequireComponent<WorldMapCamera>();
		worldMapCamera.enabled = false;
		Vector3 idealOffset = worldMapCamera.AccessIdealOffset;
		Vector3 idealLocation = _endPos + idealOffset;
		float distanceToIdeal = (idealLocation - mainCamera.transform.position).magnitude;
		float currentGradient = 0f;
		while (true)
		{
			distanceToIdeal = (idealLocation - mainCamera.transform.position).magnitude;
			MathUtils.AdvanceToTarget_Sinusoidal(_nDeltaTime: TimeManager.GetDeltaTime(base.gameObject), _nCurrentX: ref distanceToIdeal, _nCurrentGradient: ref currentGradient, _nTargetX: 0f, _nGradientLimit: m_sender.m_lerpConfig.GradientLimit, _nTimeToMax: m_sender.m_lerpConfig.TimeToMax);
			Vector3 offset = idealLocation - mainCamera.transform.position;
			Vector3 pos = idealLocation - offset.SafeNormalised(Vector3.zero) * distanceToIdeal;
			mainCamera.transform.position = pos;
			if (distanceToIdeal < 0.1f)
			{
				break;
			}
			yield return null;
		}
		worldMapCamera.enabled = true;
	}
}
