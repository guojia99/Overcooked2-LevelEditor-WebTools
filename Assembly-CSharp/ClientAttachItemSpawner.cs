using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientAttachItemSpawner : ClientSynchroniserBase
{
	private AttachItemSpawner m_attachItemSpawner;

	private IOrderDefinition m_orderDefinition;

	private ClientAttachStation m_attachStation;

	public override EntityType GetEntityType()
	{
		return EntityType.PlacementItemSpawner;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_attachItemSpawner = (AttachItemSpawner)synchronisedObject;
		m_orderDefinition = base.gameObject.RequireInterface<IOrderDefinition>();
		m_attachStation = base.gameObject.RequestComponent<ClientAttachStation>();
	}

	public override void ApplyServerEvent(Serialisable serialisable)
	{
		OnItemSpawned();
	}

	private void OnItemSpawned()
	{
		base.gameObject.SendMessage("OnPickupItem", SendMessageOptions.DontRequireReceiver);
	}
}
