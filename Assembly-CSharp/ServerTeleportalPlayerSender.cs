using System.Collections;

public class ServerTeleportalPlayerSender : ServerBaseTeleportalSender
{
	private TeleportalPlayerSender m_playerSender;

	private bool m_teleportAnimationFinished;

	protected override void Awake()
	{
		base.Awake();
		m_playerSender = base.gameObject.RequireComponent<TeleportalPlayerSender>();
		m_playerSender.RegisterAnimationFinishedCallback(OnAnimationFinished);
	}

	public override bool CanHandleTeleport(ITeleportable _object)
	{
		return _object is ServerTeleportablePlayer;
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
