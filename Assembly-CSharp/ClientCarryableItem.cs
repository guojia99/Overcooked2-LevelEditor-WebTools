using Team17.Online.Multiplayer.Messaging;

public class ClientCarryableItem : ClientSynchroniserBase, IClientHandlePickup, IHandleAttachTarget, IBaseHandlePickup
{
	public virtual PlayerAttachTarget PlayerAttachTarget
	{
		get
		{
			return PlayerAttachTarget.Default;
		}
	}

	public bool CanHandlePickup(ICarrier _carrier)
	{
		return base.isActiveAndEnabled;
	}

	public int GetPickupPriority()
	{
		return 0;
	}
}
