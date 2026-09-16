using UnityEngine;

public class ClientEverPeckishHeadCosmeticDecisions : ClientMeshVisibilityBase<EverPeckishHeadCosmeticDecisions.VisState>
{
	private EverPeckishHeadCosmeticDecisions m_cosmeticDecision;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_cosmeticDecision = (EverPeckishHeadCosmeticDecisions)synchronisedObject;
		Setup(m_cosmeticDecision.m_initialVisState);
	}

	public void SetVisState(EverPeckishHeadCosmeticDecisions.VisState _visState)
	{
		SetState(_visState);
	}
}
