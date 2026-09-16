using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerAttachItemSpawner : ServerSynchroniserBase
{
	private class HolderAdapter : ICarrier, ICarrierPlacement
	{
		private ServerAttachItemSpawner m_itemSpawner;

		public HolderAdapter(ServerAttachItemSpawner _spawner)
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

	private AttachItemSpawner m_attachItemSpawner;

	private PlacementItemSpawnerMessage m_data = new PlacementItemSpawnerMessage();

	private IOrderDefinition m_orderDefinition;

	private ServerAttachStation m_attachStation;

	private HolderAdapter m_holderAdapter;

	public override EntityType GetEntityType()
	{
		return EntityType.PlacementItemSpawner;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_attachItemSpawner = (AttachItemSpawner)synchronisedObject;
		m_orderDefinition = base.gameObject.RequireInterface<IOrderDefinition>();
		m_attachStation = base.gameObject.RequestComponent<ServerAttachStation>();
		m_attachStation.RegisterOnItemAdded(OnItemAdded);
		m_holderAdapter = new HolderAdapter(this);
	}

	public void OnItemAdded(IAttachment _iHoldable)
	{
		ServerPlacementContainer serverPlacementContainer = _iHoldable.AccessGameObject().RequestComponent<ServerPlacementContainer>();
		if (serverPlacementContainer != null)
		{
			Vector2 left = Vector2.left;
			PlacementContext context = new PlacementContext(PlacementContext.Source.Player);
			if (serverPlacementContainer.CanHandlePlacement(m_holderAdapter, left, context))
			{
				serverPlacementContainer.HandlePlacement(m_holderAdapter, left, context);
				SendServerEvent(m_data);
			}
		}
	}
}
