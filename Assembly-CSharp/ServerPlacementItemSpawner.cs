using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerPlacementItemSpawner : ServerSynchroniserBase, IHandlePlacement, IBaseHandlePlacement
{
	private class HolderAdapter : ICarrier, ICarrierPlacement
	{
		private ServerPlacementItemSpawner m_itemSpawner;

		public HolderAdapter(ServerPlacementItemSpawner _spawner)
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
		m_placementItemSpawner = (PlacementItemSpawner)synchronisedObject;
		m_orderDefinition = base.gameObject.RequireInterface<IOrderDefinition>();
		m_attachStation = base.gameObject.RequestComponent<ServerAttachStation>();
		m_holderAdapter = new HolderAdapter(this);
	}

	public bool CanHandlePlacement(ICarrier _carrier, Vector2 _directionXZ, PlacementContext _context)
	{
		ServerPlacementContainer serverPlacementContainer = _carrier.InspectCarriedItem().RequestComponent<ServerPlacementContainer>();
		if (serverPlacementContainer != null)
		{
			return serverPlacementContainer.CanHandlePlacement(m_holderAdapter, -_directionXZ, _context);
		}
		return false;
	}

	public void HandlePlacement(ICarrier _carrier, Vector2 _directionXZ, PlacementContext _context)
	{
		ServerPlacementContainer serverPlacementContainer = _carrier.InspectCarriedItem().RequestComponent<ServerPlacementContainer>();
		serverPlacementContainer.HandlePlacement(m_holderAdapter, -_directionXZ, _context);
		IngredientAssembledNode ingredientAssembledNode = m_orderDefinition.GetOrderComposition() as IngredientAssembledNode;
		if (ingredientAssembledNode != null && m_placementItemSpawner.m_condimentAchievementFilter != null && m_placementItemSpawner.m_condimentAchievementFilter.m_ids.Contains(ingredientAssembledNode.m_ingriedientOrderNode.m_uID))
		{
			ServerMessenger.Achievement(_carrier.AccessGameObject(), 803);
		}
		SendServerEvent(m_data);
	}

	public int GetPlacementPriority()
	{
		return m_placementItemSpawner.m_pickupPriority;
	}

	public void OnFailedToPlace(GameObject _item)
	{
	}
}
