using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientTeleportalCosmeticDecisions : ClientSynchroniserBase
{
	private TeleportalCosmeticDecisions m_decisions;

	private ClientTeleportal m_portal;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
	}

	private void Awake()
	{
	}

	private void OnCanTeleportStateChanged(bool _canTeleport)
	{
	}

	private void OnTeleportStateChanged(bool _teleporting)
	{
	}
}
