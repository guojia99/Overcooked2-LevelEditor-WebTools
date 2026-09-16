using UnityEngine;

[AddComponentMenu("Scripts/Game/Environment/Utensils/UsableItem")]
public class ServerUsableItem : ServerInteractable, ICarryNotified
{
	private PlayerControls m_controls;

	public override bool CanInteract(GameObject _interacter)
	{
		IPlayerCarrier playerCarrier = _interacter.RequireInterface<IPlayerCarrier>();
		GameObject gameObject = playerCarrier.InspectCarriedItem();
		return base.CanInteract(_interacter) && gameObject == base.gameObject;
	}

	public override bool InteractionIsSticky()
	{
		return base.InteractionIsSticky() || !UnderDirectControl();
	}

	private bool UnderDirectControl()
	{
		return m_controls != null && m_controls.GetDirectlyUnderPlayerControl();
	}

	public virtual void OnCarryBegun(ICarrier _carrier)
	{
		GameObject obj = (_carrier as MonoBehaviour).gameObject;
		m_controls = obj.RequestComponent<PlayerControls>();
	}

	public virtual void OnCarryEnded(ICarrier _carrier)
	{
		m_controls = null;
	}
}
