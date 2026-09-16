using UnityEngine;

public class ClientHeldItemMeshVisibility : ClientMeshVisibilityBase<HeldItemMeshVisibility.VisState>, ICarryNotified
{
	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		Setup(HeldItemMeshVisibility.VisState.NotCarrying);
	}

	public void SetVisState(HeldItemMeshVisibility.VisState _visState)
	{
		SetState(_visState);
	}

	public void OnCarryBegun(ICarrier _carrier)
	{
		SetVisState(HeldItemMeshVisibility.VisState.Carrying);
	}

	public void OnCarryEnded(ICarrier _carrier)
	{
		SetVisState(HeldItemMeshVisibility.VisState.NotCarrying);
	}
}
