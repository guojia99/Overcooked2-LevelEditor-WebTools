using UnityEngine;

public class ClientUsableItem : ClientInteractable
{
	public override bool CanInteract(GameObject _interacter)
	{
		IPlayerCarrier playerCarrier = _interacter.RequireInterface<IPlayerCarrier>();
		GameObject gameObject = playerCarrier.InspectCarriedItem();
		return base.CanInteract(_interacter) && gameObject == base.gameObject;
	}
}
