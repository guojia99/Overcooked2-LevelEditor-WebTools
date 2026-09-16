using System;
using System.Collections.Generic;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerAttachStation : ServerSynchroniserBase, IHandlePlacement, IHandlePickup, IPlacementSupression, IHandleCatch, IBaseHandlePlacement, IBaseHandlePickup
{
	public delegate void OnItemAdded(IAttachment _iHoldable);

	public delegate void OnItemRemoved(IAttachment _iHoldable);

	public class HolderAdapter : ICarrier, ICarrierPlacement
	{
		private ServerAttachStation m_attachStation;

		public HolderAdapter(ServerAttachStation _station)
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
			GameObject gameObject = TakeItem();
			NetworkUtils.DestroyObject(gameObject);
		}

		public void CarryItem(GameObject _object)
		{
			m_attachStation.AddItem(_object, m_attachStation.transform.forward.XZ());
		}

		public GameObject TakeItem()
		{
			return m_attachStation.TakeItem();
		}
	}

	private AttachStation m_station;

	private AttachStationMessage m_data = new AttachStationMessage();

	private bool m_pendingInit;

	private ServerAttachmentCatchingProxy m_attachmentCatchingProxy;

	private IAttachment m_item;

	private OnItemAdded m_itemAdded = delegate
	{
	};

	private OnItemRemoved m_itemRemoved = delegate
	{
	};

	private List<Generic<bool, GameObject, PlacementContext>> m_allowPlacementCallbacks = new List<Generic<bool, GameObject, PlacementContext>>();

	private List<Generic<bool>> m_allowPickupCallbacks = new List<Generic<bool>>();

	private VoidGeneric<GameObject> m_failedToPlaceCallback = delegate
	{
	};

	private HolderAdapter m_holderAdapter;

	public override EntityType GetEntityType()
	{
		return EntityType.AttachStation;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_station = (AttachStation)synchronisedObject;
		m_pendingInit = true;
		m_attachmentCatchingProxy = base.gameObject.RequestComponent<ServerAttachmentCatchingProxy>();
		if (m_attachmentCatchingProxy != null)
		{
			m_attachmentCatchingProxy.RegisterUncatchableItemCallback(HandleUncatchableItem);
		}
	}

	private void SendItemEvent()
	{
		m_data.m_item = ((m_item == null) ? null : m_item.AccessGameObject());
		SendServerEvent(m_data);
	}

	public bool CanHandlePickup(ICarrier _carrier)
	{
		if (m_item != null)
		{
			if (m_allowPickupCallbacks.CallForResult(false))
			{
				return false;
			}
			IHandlePickup handlePickup = m_item.AccessGameObject().RequireInterface<IHandlePickup>();
			return handlePickup.CanHandlePickup(_carrier);
		}
		return false;
	}

	public void HandlePickup(ICarrier _carrier, Vector2 _directionXZ)
	{
		IHandlePickup handlePickup = m_item.AccessGameObject().RequireInterface<IHandlePickup>();
		bool itemTaken = false;
		AttachChangedCallback callback = delegate
		{
			itemTaken = true;
		};
		m_item.RegisterAttachChangedCallback(callback);
		handlePickup.HandlePickup(_carrier, _directionXZ);
		m_item.UnregisterAttachChangedCallback(callback);
		if (itemTaken)
		{
			OnItemTaken();
		}
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
		IHandlePlacement placementHandler = null;
		if (gameObject != null)
		{
			placementHandler = gameObject.RequestInterface<IHandlePlacement>();
		}
		return m_station.CalculatePlacementType<IHandlePlacement>(gameObject, _context, _iCarrier, _directionXZ, placementHandler, m_allowPlacementCallbacks, GetHolder()) != PlacementType.NotValid;
	}

	public bool CanAttachToSelf(GameObject _item, PlacementContext _context = default(PlacementContext))
	{
		return m_station.CanAttachToSelf((m_item == null) ? null : m_item.AccessGameObject(), _item, _context, m_allowPlacementCallbacks);
	}

	public void HandlePlacement(ICarrier _iCarrier, Vector2 _directionXZ, PlacementContext _context)
	{
		GameObject gameObject = ((m_item == null) ? null : m_item.AccessGameObject());
		IHandlePlacement placementHandler = null;
		if (gameObject != null)
		{
			placementHandler = gameObject.RequestInterface<IHandlePlacement>();
		}
		switch (m_station.CalculatePlacementType<IHandlePlacement>(gameObject, _context, _iCarrier, _directionXZ, placementHandler, m_allowPlacementCallbacks, GetHolder()))
		{
		case PlacementType.OntoEmpty:
			OnItemPlaced(_iCarrier.TakeItem(), _directionXZ, _context);
			break;
		case PlacementType.ContentsOntoEmpty:
			OnContentsPlaced(_iCarrier.InspectCarriedItem(), _directionXZ, _context);
			break;
		case PlacementType.OntoOccupant:
			PlaceOntoOccupant(_iCarrier, _directionXZ, _context);
			break;
		case PlacementType.UnderOccupant:
			PlaceUnderOccupant(_iCarrier, _directionXZ, _context);
			break;
		case PlacementType.OntoAndUnderOccupant:
			PlaceOntoOccupant(_iCarrier, _directionXZ, _context);
			PlaceUnderOccupant(_iCarrier, _directionXZ, _context);
			break;
		}
	}

	private void OnContentsPlaced(GameObject _container, Vector2 _directionXZ, PlacementContext _context)
	{
		ServerIngredientContainer serverIngredientContainer = _container.RequireComponent<ServerIngredientContainer>();
		if (serverIngredientContainer != null)
		{
			AssembledDefinitionNode assembledDefinitionNode = serverIngredientContainer.GetContents()[0];
			GameObject freeObject = assembledDefinitionNode.m_freeObject;
			serverIngredientContainer.Empty();
			OnItemPlaced(freeObject, _directionXZ, _context);
			freeObject.SetActive(true);
		}
	}

	public void RegisterFailedToPlace(VoidGeneric<GameObject> _callback)
	{
		m_failedToPlaceCallback = (VoidGeneric<GameObject>)Delegate.Combine(m_failedToPlaceCallback, _callback);
	}

	public void UnregisterFailedToPlace(VoidGeneric<GameObject> _callback)
	{
		m_failedToPlaceCallback = (VoidGeneric<GameObject>)Delegate.Remove(m_failedToPlaceCallback, _callback);
	}

	public void OnFailedToPlace(GameObject _item)
	{
		m_failedToPlaceCallback(_item);
	}

	private void PlaceOntoOccupant(ICarrier _iCarrier, Vector2 _directionXZ, PlacementContext _context)
	{
		IHandlePlacement handlePlacement = m_item.AccessGameObject().RequireInterface<IHandlePlacement>();
		handlePlacement.HandlePlacement(_iCarrier, _directionXZ, _context);
	}

	private void PlaceUnderOccupant(ICarrier _iCarrier, Vector2 _directionXZ, PlacementContext _context)
	{
		GameObject obj = _iCarrier.InspectCarriedItem();
		ICarrier holder = GetHolder();
		if (holder.InspectCarriedItem() != null)
		{
			IHandlePlacement handlePlacement = obj.RequireInterface<IHandlePlacement>();
			handlePlacement.HandlePlacement(GetHolder(), _directionXZ, _context);
		}
		OnItemPlaced(_iCarrier.TakeItem(), _directionXZ, _context);
	}

	public int GetPlacementPriority()
	{
		if (m_item == null)
		{
			return m_station.m_placementPriority;
		}
		return int.MinValue;
	}

	public Transform GetAttachPoint(GameObject gameObject)
	{
		return m_station.GetAttachPoint(gameObject);
	}

	public bool HasItem()
	{
		return m_item != null;
	}

	public GameObject InspectItem()
	{
		return (m_item == null) ? null : m_item.AccessGameObject();
	}

	public GameObject TakeItem()
	{
		GameObject result = m_item.AccessGameObject();
		m_item.Detach();
		OnItemTaken();
		return result;
	}

	public void AddItem(GameObject _item, Vector2 _directionXZ, PlacementContext _context = default(PlacementContext))
	{
		OnItemPlaced(_item, _directionXZ, _context);
	}

	private void OnItemTaken()
	{
		ServerHandlePickupReferral component = m_item.AccessGameObject().GetComponent<ServerHandlePickupReferral>();
		if ((bool)component && component.GetHandlePickupReferree() == this)
		{
			component.SetHandlePickupReferree(null);
		}
		ServerHandlePlacementReferral component2 = m_item.AccessGameObject().GetComponent<ServerHandlePlacementReferral>();
		if ((bool)component2 && component2.GetHandlePlacementReferree() == this)
		{
			component2.SetHandlePlacementReferree(null);
		}
		ServerAttachmentCatchingProxy component3 = m_item.AccessGameObject().GetComponent<ServerAttachmentCatchingProxy>();
		if ((bool)component3 && component3.GetHandleCatchingReferree() == this)
		{
			component3.SetHandleCatchingReferree(null);
		}
		ISurfacePlacementNotified[] array = m_item.AccessGameObject().RequestInterfaces<ISurfacePlacementNotified>();
		foreach (ISurfacePlacementNotified surfacePlacementNotified in array)
		{
			surfacePlacementNotified.OnSurfaceDeplacement(this);
		}
		IAttachment item = m_item;
		ServerLimitedQuantityItem component4 = m_item.AccessGameObject().GetComponent<ServerLimitedQuantityItem>();
		if (null != component4)
		{
			component4.UnregisterImpendingDestructionNotification(OnAttachmentDestroyed);
		}
		m_item = null;
		SendItemEvent();
		m_itemRemoved(item);
	}

	private void OnItemPlaced(GameObject _objectToPlace, Vector2 _directionXZ, PlacementContext _context)
	{
		IAttachment component = _objectToPlace.GetComponent<IAttachment>();
		component.Attach(m_station);
		component.AccessGameObject().transform.localRotation = GetRotationOnSurface(_directionXZ);
		m_item = component;
		ServerLimitedQuantityItem component2 = m_item.AccessGameObject().GetComponent<ServerLimitedQuantityItem>();
		if (null != component2)
		{
			component2.RegisterImpendingDestructionNotification(OnAttachmentDestroyed);
		}
		ServerHandlePickupReferral component3 = m_item.AccessGameObject().GetComponent<ServerHandlePickupReferral>();
		if ((bool)component3)
		{
			component3.SetHandlePickupReferree(this);
		}
		ServerHandlePlacementReferral component4 = m_item.AccessGameObject().GetComponent<ServerHandlePlacementReferral>();
		if ((bool)component4)
		{
			component4.SetHandlePlacementReferree(this);
		}
		ServerAttachmentCatchingProxy component5 = m_item.AccessGameObject().GetComponent<ServerAttachmentCatchingProxy>();
		if ((bool)component5)
		{
			component5.SetHandleCatchingReferree(this);
		}
		ISurfacePlacementNotified[] array = m_item.AccessGameObject().RequestInterfaces<ISurfacePlacementNotified>();
		foreach (ISurfacePlacementNotified surfacePlacementNotified in array)
		{
			surfacePlacementNotified.OnSurfacePlacement(this);
		}
		m_itemAdded(component);
		SendItemEvent();
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

	public void RotateForDirection(Vector2 _playerDirectionXZ)
	{
		m_item.AccessGameObject().transform.localRotation = GetRotationOnSurface(_playerDirectionXZ);
	}

	private Quaternion GetRotationOnSurface(Vector2 _playerDirectionXZ)
	{
		Quaternion rot = Quaternion.LookRotation(VectorUtils.FromXZ(_playerDirectionXZ, 0f), Vector3.up);
		TransformHelper trans = new TransformHelper(rot, Vector3.zero);
		return new TransformHelper(m_station.GetAttachPoint(base.gameObject)).ToLocal(trans).Rotation;
	}

	private void AttachInitialObjects()
	{
		Collider collider = base.gameObject.RequireComponent<Collider>();
		Bounds bounds = collider.bounds;
		float magnitude = bounds.extents.magnitude;
		Collider[] array = Physics.OverlapSphere(bounds.center, magnitude);
		foreach (Collider collider2 in array)
		{
			if (collider2.gameObject != collider.gameObject && bounds.Contains(collider2.bounds.center))
			{
				IAttachment attachment = collider2.gameObject.RequestInterface<IAttachment>();
				if (attachment != null && !attachment.IsAttached() && CanAttachToSelf(collider2.gameObject))
				{
					AddItem(collider2.gameObject, collider2.transform.forward.XZ());
				}
			}
		}
	}

	private void Awake()
	{
		m_holderAdapter = new HolderAdapter(this);
	}

	public override void OnDestroy()
	{
		base.OnDestroy();
		if (m_attachmentCatchingProxy != null)
		{
			m_attachmentCatchingProxy.UnRegisterUncatchableItemCallback(HandleUncatchableItem);
		}
	}

	public override void UpdateSynchronising()
	{
		if (m_pendingInit)
		{
			m_pendingInit = false;
			AttachInitialObjects();
		}
	}

	private void AttachObject(GameObject _object, PlacementContext _context = default(PlacementContext))
	{
		if (CanAttachToSelf(_object.gameObject, _context))
		{
			AddItem(_object.gameObject, (_object.transform.position - base.transform.position).SafeNormalised(base.transform.forward).XZ(), _context);
		}
	}

	private void HandleUncatchableItem(GameObject _object, Vector2 _directionXZ)
	{
		AttachObject(_object);
	}

	public bool CanHandleCatch(ICatchable _object, Vector2 _directionXZ)
	{
		if (!_object.AllowCatch(this, _directionXZ))
		{
			return false;
		}
		if (HasItem())
		{
			IHandleCatch handleCatch = m_item.AccessGameObject().RequestInterface<IHandleCatch>();
			if (handleCatch != null)
			{
				return handleCatch.CanHandleCatch(_object, _directionXZ);
			}
		}
		IThrowable throwable = _object.AccessGameObject().RequestInterface<IThrowable>();
		if (throwable != null && !throwable.IsFlying())
		{
			if (!m_station.m_canCatch)
			{
				return false;
			}
			return CanAttachToSelf(_object.AccessGameObject(), new PlacementContext(PlacementContext.Source.Game));
		}
		return false;
	}

	public void HandleCatch(ICatchable _object, Vector2 _directionXZ)
	{
		if (HasItem())
		{
			IHandleCatch handleCatch = m_item.AccessGameObject().RequestInterface<IHandleCatch>();
			if (handleCatch != null)
			{
				handleCatch.HandleCatch(_object, _directionXZ);
			}
		}
		else
		{
			AttachObject(_object.AccessGameObject());
		}
	}

	public void AlertToThrownItem(ICatchable _thrown, IThrower _thrower, Vector2 _directionXZ)
	{
	}

	public int GetCatchingPriority()
	{
		if (m_item != null)
		{
			return m_station.m_catchingPriority;
		}
		return int.MinValue;
	}

	private void OnAttachmentDestroyed(GameObject toDeDestroyed)
	{
		IAttachment attachment = toDeDestroyed.RequestInterface<IAttachment>();
		if (m_item != null && attachment == m_item)
		{
			TakeItem();
		}
	}

	private ICarrier GetHolder()
	{
		return m_holderAdapter;
	}
}
