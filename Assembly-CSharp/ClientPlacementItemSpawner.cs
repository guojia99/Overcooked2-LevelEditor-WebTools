using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientPlacementItemSpawner : ClientSynchroniserBase, IClientHandlePlacement, IBaseHandlePlacement
{
	private class HolderAdapter : ICarrier, ICarrierPlacement
	{
		private ClientPlacementItemSpawner m_itemSpawner;

		public HolderAdapter(ClientPlacementItemSpawner _spawner)
		{
			m_itemSpawner = _spawner;
		}

		public void RegisterCarriedItemChangeCallback(VoidGeneric<GameObject, GameObject> _callback)
		{
		}

		public void UnregisterCarriedItemChangeCallback(VoidGeneric<GameObject, GameObject> _callback)
		{
		}

		public void CarryItem(GameObject _object)
		{
		}

		public GameObject TakeItem()
		{
			return null;
		}

		public void DestroyCarriedItem()
		{
		}

		public GameObject InspectCarriedItem()
		{
			return m_itemSpawner.gameObject;
		}

		public GameObject AccessGameObject()
		{
			return m_itemSpawner.gameObject;
		}
	}

	private PlacementItemSpawner m_placementItemSpawner;

	private IOrderDefinition m_orderDefinition;

	private ClientAttachStation m_attachStation;

	private HolderAdapter m_holderAdapter;

	public override EntityType GetEntityType()
	{
		return EntityType.PlacementItemSpawner;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_placementItemSpawner = (PlacementItemSpawner)synchronisedObject;
		m_orderDefinition = base.gameObject.RequireInterface<IOrderDefinition>();
		m_attachStation = base.gameObject.RequestComponent<ClientAttachStation>();
		m_holderAdapter = new HolderAdapter(this);
	}

	public override void ApplyServerEvent(Serialisable serialisable)
	{
		OnItemSpawned();
	}

	private void OnItemSpawned()
	{
		base.gameObject.SendMessage("OnPickupItem", SendMessageOptions.DontRequireReceiver);
	}

	public bool CanHandlePlacement(ICarrier _carrier, Vector2 _directionXZ, PlacementContext _context)
	{
		ClientPlacementContainer clientPlacementContainer = _carrier.InspectCarriedItem().RequestComponent<ClientPlacementContainer>();
		if (clientPlacementContainer != null)
		{
			return clientPlacementContainer.CanHandlePlacement(m_holderAdapter, -_directionXZ, _context);
		}
		return false;
	}

	public int GetPlacementPriority()
	{
		return m_placementItemSpawner.m_pickupPriority;
	}
}
