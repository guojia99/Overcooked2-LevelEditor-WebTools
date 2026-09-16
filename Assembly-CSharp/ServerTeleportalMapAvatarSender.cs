using System.Collections;

public class ServerTeleportalMapAvatarSender : ServerBaseTeleportalSender
{
	private TeleportalMapAvatarSender m_mapAvatarSender;

	private bool m_teleportAnimationFinished;

	protected override void Awake()
	{
		base.Awake();
		m_mapAvatarSender = base.gameObject.RequireComponent<TeleportalMapAvatarSender>();
		m_mapAvatarSender.RegisterAnimationFinishedCallback(OnAnimationFinished);
	}

	public override bool CanHandleTeleport(ITeleportable _object)
	{
		return _object is ServerTeleportableMapAvatar;
	}

	protected override IEnumerator TeleportRoutine(ServerTeleportal _entrancePortal, ITeleportalReceiver _receiver, ITeleportable _object)
	{
		m_teleportAnimationFinished = false;
		while (!m_teleportAnimationFinished)
		{
			yield return null;
		}
	}

	public void OnAnimationFinished(string _animName)
	{
		if (_animName == "Teleport")
		{
			m_teleportAnimationFinished = true;
		}
	}
}
