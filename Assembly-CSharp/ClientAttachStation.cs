using System;
using System.Collections.Generic;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientAttachStation : ClientSynchroniserBase, IClientHandlePickup, IClientHandlePlacement, IClientHandleCatch, IBaseHandlePickup, IBaseHandlePlacement
{
	public class HolderAdapter : ICarrier, ICarrierPlacement
	{
		private ClientAttachStation m_attachStation;

		public HolderAdapter(ClientAttachStation _station)
		{
			m_attachStation = _station;
		}

		public void RegisterCarriedItemChangeCallback(VoidGeneric<GameObject, GameObject> _callback)
		{
		}

		public void UnregisterCarriedItemChangeCallback(VoidGeneric<GameObject, GameObject> _callback)
		{
		}

		public GameObject InspectCarriedItem()
		{
			return m_attachStation.InspectItem();
		}

		public GameObject AccessGameObject()
		{
			return m_attachStation.gameObject;
		}

		public void DestroyCarriedItem()
		{
		}

		public void CarryItem(GameObject _object)
		{
		}

		public GameObject TakeItem()
		{
			return null;
		}
	}

	public delegate void OnItemAdded(IClientAttachment _iHoldable);

	public delegate void OnItemRemoved(IClientAttachment _iHoldable);

	private HolderAdapter m_holderAdapter;

	private AttachStation m_station;

	private IClientAttachment m_item;

	private OnItemAdded m_itemAdded = delegate
	{
	};

	private OnItemRemoved m_itemRemoved = delegate
	{
	};

	private List<Generic<bool>> m_allowPickupCallbacks = new List<Generic<bool>>();

	private List<Generic<bool, GameObject, PlacementContext>> m_allowPlacementCallbacks = new List<Generic<bool, GameObject, PlacementContext>>();

	public override EntityType GetEntityType()
	{
		return EntityType.AttachStation;
	}

	public override void ApplyServerEvent(Serialisable serialisable)
	{
		AttachStationMessage attachStationMessage = (AttachStationMessage)serialisable;
		MonoBehaviour monoBehaviour = m_item as MonoBehaviour;
		GameObject gameObject = ((!(monoBehaviour != null)) ? null : m_item.AccessGameObject());
		if ((monoBehaviour == null && attachStationMessage.m_item != null) || (monoBehaviour != null && gameObject != attachStationMessage.m_item))
		{
			if (attachStationMessage.m_item != null)
			{
				OnItemPlaced(attachStationMessage.m_item.RequireInterface<IClientAttachment>());
			}
			else
			{
				OnItemTaken();
			}
		}
	}

	public void RegisterOnItemAdded(OnItemAdded _added)
	{
		m_itemAdded = (OnItemAdded)Delegate.Combine(m_itemAdded, _added);
	}

	public void UnregisterOnItemAdded(OnItemAdded _added)
	{
		m_itemAdded = (OnItemAdded)Delegate.Remove(m_itemAdded, _added);
	}

	public void RegisterOnItemRemoved(OnItemRemoved _removed)
	{
		m_itemRemoved = (OnItemRemoved)Delegate.Combine(m_itemRemoved, _removed);
	}

	public void UnregisterOnItemRemoved(OnItemRemoved _removed)
	{
		m_itemRemoved = (OnItemRemoved)Delegate.Remove(m_itemRemoved, _removed);
	}

	public void RegisterAllowItemPickup(Generic<bool> _allowPickupCallback)
	{
		m_allowPickupCallbacks.Add(_allowPickupCallback);
	}

	public void UnregisterAllowItemPickup(Generic<bool> _allowPickupCallback)
	{
		m_allowPickupCallbacks.Remove(_allowPickupCallback);
	}

	public void RegisterAllowItemPlacement(Generic<bool, GameObject, PlacementContext> _allowPlacementCallback)
	{
		m_allowPlacementCallbacks.Add(_allowPlacementCallback);
	}

	public void UnregisterAllowItemPlacement(Generic<bool, GameObject, PlacementContext> _allowPlacementCallback)
	{
		m_allowPlacementCallbacks.Remove(_allowPlacementCallback);
	}

	private void Awake()
	{
		m_station = base.gameObject.RequireComponent<AttachStation>();
		m_holderAdapter = new HolderAdapter(this);
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
	}

	public bool HasItem()
	{
		return m_item != null;
	}

	public GameObject InspectItem()
	{
		return (m_item == null) ? null : m_item.AccessGameObject();
	}

	private void OnItemPlaced(IClientAttachment _item)
	{
		m_item = _item;
		ClientLimitedQuantityItem component = m_item.AccessGameObject().GetComponent<ClientLimitedQuantityItem>();
		if (null != component)
		{
			component.RegisterImpendingDestructionNotification(OnAttachmentDestroyed);
		}
		ClientHandlePickupReferral component2 = m_item.AccessGameObject().GetComponent<ClientHandlePickupReferral>();
		if ((bool)component2)
		{
			component2.SetHandlePickupReferree(this);
		}
		ClientHandlePlacementReferral clientHandlePlacementReferral = m_item.AccessGameObject().RequestComponent<ClientHandlePlacementReferral>();
		if (clientHandlePlacementReferral != null)
		{
			clientHandlePlacementReferral.SetHandlePlacementReferree(this);
		}
		IClientSurfacePlacementNotified[] array = m_item.AccessGameObject().RequestInterfaces<IClientSurfacePlacementNotified>();
		foreach (IClientSurfacePlacementNotified clientSurfacePlacementNotified in array)
		{
			clientSurfacePlacementNotified.OnSurfacePlacement(this);
		}
		m_itemAdded(m_item);
	}

	private void OnItemTaken()
	{
		IClientSurfacePlacementNotified[] array = m_item.AccessGameObject().RequestInterfaces<IClientSurfacePlacementNotified>();
		foreach (IClientSurfacePlacementNotified clientSurfacePlacementNotified in array)
		{
			clientSurfacePlacementNotified.OnSurfaceDeplacement(this);
		}
		IClientAttachment item = m_item;
		m_item = null;
		ClientLimitedQuantityItem component = item.AccessGameObject().GetComponent<ClientLimitedQuantityItem>();
		if (null != component)
		{
			component.UnregisterImpendingDestructionNotification(OnAttachmentDestroyed);
		}
		m_itemRemoved(item);
		ClientHandlePickupReferral component2 = item.AccessGameObject().GetComponent<ClientHandlePickupReferral>();
		if ((bool)component2 && component2.GetHandlePickupReferree() == this)
		{
			component2.SetHandlePickupReferree(null);
		}
		ClientHandlePlacementReferral clientHandlePlacementReferral = item.AccessGameObject().RequestComponent<ClientHandlePlacementReferral>();
		if (clientHandlePlacementReferral != null && clientHandlePlacementReferral.GetHandlePlacementReferree() == this)
		{
			clientHandlePlacementReferral.SetHandlePlacementReferree(null);
		}
	}

	private void OnAttachmentDestroyed(GameObject toDeDestroyed)
	{
		IClientAttachment clientAttachment = toDeDestroyed.RequestInterface<IClientAttachment>();
		if (m_item != null && clientAttachment == m_item)
		{
			OnItemTaken();
		}
	}

	public bool CanHandlePickup(ICarrier _carrier)
	{
		MonoBehaviour monoBehaviour = m_item as MonoBehaviour;
		if (monoBehaviour != null)
		{
			if (m_allowPickupCallbacks.CallForResult(false))
			{
				return false;
			}
			IClientHandlePickup clientHandlePickup = m_item.AccessGameObject().RequireInterface<IClientHandlePickup>();
			return clientHandlePickup.CanHandlePickup(_carrier);
		}
		return false;
	}

	public int GetPickupPriority()
	{
		if (m_item != null)
		{
			return m_station.m_pickupPriority;
		}
		return int.MinValue;
	}

	public bool CanHandlePlacement(ICarrier _iCarrier, Vector2 _directionXZ, PlacementContext _context)
	{
		GameObject gameObject = ((m_item == null) ? null : m_item.AccessGameObject());
		IClientHandlePlacement placementHandler = null;
		if (gameObject != null)
		{
			placementHandler = gameObject.RequestInterface<IClientHandlePlacement>();
		}
		return m_station.CalculatePlacementType<IClientHandlePlacement>(gameObject, _context, _iCarrier, _directionXZ, placementHandler, m_allowPlacementCallbacks, GetHolder()) != PlacementType.NotValid;
	}

	public int GetPlacementPriority()
	{
		if (m_item == null && m_station != null)
		{
			return m_station.m_placementPriority;
		}
		return int.MinValue;
	}

	private ICarrier GetHolder()
	{
		return m_holderAdapter;
	}

	public Transform GetAttachPoint(GameObject gameObject)
	{
		return m_station.GetAttachPoint(gameObject);
	}
}
