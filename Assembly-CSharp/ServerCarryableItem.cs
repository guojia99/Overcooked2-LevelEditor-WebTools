using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerCarryableItem : ServerSynchroniserBase, IHandlePickup, IHandleAttachTarget, IBaseHandlePickup
{
	public virtual PlayerAttachTarget PlayerAttachTarget
	{
		get
		{
			return PlayerAttachTarget.Default;
		}
	}

	public virtual bool CanHandlePickup(ICarrier _carrier)
	{
		return base.isActiveAndEnabled;
	}

	public virtual void HandlePickup(ICarrier _carrier, Vector2 _directionXZ)
	{
		IAttachment component = base.gameObject.GetComponent<IAttachment>();
		if (component.IsAttached())
		{
			component.Detach();
		}
		_carrier.CarryItem(base.gameObject);
	}

	public int GetPickupPriority()
	{
		return 0;
	}
}
