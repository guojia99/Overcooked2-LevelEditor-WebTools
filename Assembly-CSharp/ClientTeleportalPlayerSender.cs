using System.Collections;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientTeleportalPlayerSender : ClientBaseTeleportalSender
{
	private TeleportalPlayerSender m_sender;

	private static readonly int m_iTeleport = Animator.StringToHash("Teleport");

	private static readonly int m_iIsFinished = Animator.StringToHash("IsFinished");

	private static readonly int m_iReset = Animator.StringToHash("Reset");

	protected override void Awake()
	{
		base.Awake();
		m_sender = base.gameObject.RequireComponent<TeleportalPlayerSender>();
	}

	protected override IEnumerator TeleportRoutine(ClientTeleportal _entrancePortal, IClientTeleportalReceiver _receiver, IClientTeleportable _object)
	{
		GameObject root = ((MonoBehaviour)_object).gameObject;
		PlayerControls controls = root.RequireComponent<PlayerControls>();
		root = controls.gameObject;
		controls.enabled = false;
		controls.Motion.SetKinematic(true);
		OvercookedAchievementManager achievements = GameUtils.RequestManager<OvercookedAchievementManager>();
		PlayerIDProvider provider = root.RequestComponent<PlayerIDProvider>();
		if (achievements != null && provider != null)
		{
			ControlPadInput.PadNum padForPlayer = PlayerInputLookup.GetPadForPlayer(provider.GetID());
			achievements.IncStat(6, 1f, padForPlayer);
		}
		Collider collider = root.RequireComponent<Collider>();
		collider.enabled = false;
		DynamicLandscapeParenting dynamicParenting = root.RequestComponent<DynamicLandscapeParenting>();
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
		Animator anim = root.GetComponentInChildren<Animator>(true);
		anim.gameObject.SetActive(false);
	}
}
