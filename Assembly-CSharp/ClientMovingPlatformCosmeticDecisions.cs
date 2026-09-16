using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientMovingPlatformCosmeticDecisions : ClientSynchroniserBase
{
	private MovingPlatformCosmeticDecisions m_decisions;

	private ClientPilotMovement m_clientPilotMovement;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_decisions = (MovingPlatformCosmeticDecisions)synchronisedObject;
		m_clientPilotMovement = base.gameObject.RequireComponent<ClientPilotMovement>();
		m_clientPilotMovement.OnPilotStatusChanged += OnPilotStatusChanged;
	}

	private void OnPilotStatusChanged(bool _hasPilot)
	{
		m_decisions.OnPilotStatusChanged(_hasPilot);
	}
}
