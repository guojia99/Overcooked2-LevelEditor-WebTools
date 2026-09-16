using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerCannonCosmeticDecisions : ServerSynchroniserBase
{
	private CannonCosmeticDecisions m_cannonCosmeticDecisions;

	private ServerCannon m_cannon;

	private const string m_readyStateName = "DLC08_Cannon_Ready";

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_cannonCosmeticDecisions = (CannonCosmeticDecisions)synchronisedObject;
		m_cannon = base.gameObject.RequireComponent<ServerCannon>();
		m_cannon.SetReadyToLaunchCallback(IsReadyToLaunch);
	}

	private bool IsReadyToLaunch()
	{
		return m_cannonCosmeticDecisions.m_cannonAnimator.GetCurrentAnimatorStateInfo(0).IsName("DLC08_Cannon_Ready");
	}
}
