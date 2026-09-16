using System.Collections;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerTeleportalMapAvatarReceiver : ServerBaseTeleportalReceiver
{
	private TeleportalMapAvatarReceiver m_mapAvatarReceiver;

	private bool m_teleportAnimationFinished;

	private Animator m_animator;

	protected override void Awake()
	{
		base.Awake();
		m_mapAvatarReceiver = base.gameObject.RequireComponent<TeleportalMapAvatarReceiver>();
		m_mapAvatarReceiver.RegisterAnimationFinishedCallback(OnAnimationFinished);
	}

	public override bool CanHandleTeleport(ITeleportable _object)
	{
		return _object is ServerTeleportableMapAvatar;
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
			MapAvatarGroundCast mapAvatarGroundCast = obj.RequestComponent<MapAvatarGroundCast>();
			if (mapAvatarGroundCast != null)
			{
				mapAvatarGroundCast.ForceUpdateNow();
			}
			ServerWorldObjectSynchroniser serverWorldObjectSynchroniser = obj.RequestComponent<ServerWorldObjectSynchroniser>();
			if (serverWorldObjectSynchroniser != null)
			{
				serverWorldObjectSynchroniser.ResumeAllClients();
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
