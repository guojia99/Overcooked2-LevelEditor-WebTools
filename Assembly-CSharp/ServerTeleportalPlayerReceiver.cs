using System.Collections;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerTeleportalPlayerReceiver : ServerBaseTeleportalReceiver
{
	private TeleportalPlayerReceiver m_playerReceiver;

	private bool m_teleportAnimationFinished;

	private Animator m_animator;

	protected override void Awake()
	{
		base.Awake();
		m_playerReceiver = base.gameObject.RequireComponent<TeleportalPlayerReceiver>();
		m_playerReceiver.RegisterAnimationFinishedCallback(OnAnimationFinished);
	}

	public override bool CanHandleTeleport(ITeleportable _object)
	{
		return _object is ServerTeleportablePlayer;
	}

	protected override IEnumerator TeleportRoutine(ServerTeleportal _entrancePortal, ITeleportalSender _sender, ITeleportable _object)
	{
		m_teleportAnimationFinished = false;
		while (!m_teleportAnimationFinished)
		{
			yield return null;
		}
		if (_object != null && (MonoBehaviour)_object != null)
		{
			GameObject obj = ((MonoBehaviour)_object).gameObject;
			GroundCast groundCast = obj.RequestComponent<GroundCast>();
			if (groundCast != null)
			{
				groundCast.ForceUpdateNow();
			}
			ServerWorldObjectSynchroniser serverWorldObjectSynchroniser = obj.RequestComponent<ServerWorldObjectSynchroniser>();
			if (serverWorldObjectSynchroniser != null)
			{
				serverWorldObjectSynchroniser.ResumeAllClients(false);
			}
		}
	}

	public void OnAnimationFinished(string _animName)
	{
		if (_animName == "Receive")
		{
			m_teleportAnimationFinished = true;
		}
	}
}
