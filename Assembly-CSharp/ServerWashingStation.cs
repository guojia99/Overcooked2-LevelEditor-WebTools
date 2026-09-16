using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerWashingStation : ServerSynchroniserBase, IHandlePlacement, IBaseHandlePlacement
{
	private WashingStation m_washingStation;

	private ServerAttachStation m_serverAttachStation;

	private ServerAttachStation m_dryingAttachStation;

	private WashingStationMessage m_data = new WashingStationMessage();

	private ServerInteractable m_interactable;

	private float m_cleaningTimer;

	private int m_plateCount;

	private GameObject m_interactor;

	private ServerHandlePickupReferral m_handlePickupReferral;

	private IHandlePickup m_originalPickupReferee;

	private ServerPlateReturnStation m_plateReturnStation;

	public override EntityType GetEntityType()
	{
		return EntityType.WashingStation;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_washingStation = (WashingStation)synchronisedObject;
		m_serverAttachStation = base.gameObject.RequireComponent<ServerAttachStation>();
		m_serverAttachStation.RegisterOnItemAdded(OnItemAdded);
		m_serverAttachStation.RegisterOnItemRemoved(OnItemRemoved);
		m_handlePickupReferral = base.gameObject.RequireComponent<ServerHandlePickupReferral>();
		m_originalPickupReferee = m_handlePickupReferral.GetHandlePickupReferree();
		m_interactable = base.gameObject.GetComponent<ServerInteractable>();
		m_interactable.RegisterCallbacks(OnInteracterAdded, OnInteracterRemoved);
		m_interactable.enabled = false;
		m_interactable.SetStickyInteractionCallback(() => true);
		m_plateReturnStation = m_washingStation.m_dryingStation.gameObject.RequireComponent<ServerPlateReturnStation>();
		m_dryingAttachStation = m_washingStation.m_dryingStation.gameObject.RequireComponent<ServerAttachStation>();
		m_dryingAttachStation.RegisterOnItemAdded(OnItemAddedDryingStation);
		m_dryingAttachStation.RegisterOnItemRemoved(OnItemRemovedDryingStation);
	}

	private void SynchroniseInteractionState(bool _interacting)
	{
		m_data.m_msgType = WashingStationMessage.MessageType.InteractionState;
		m_data.m_interacting = _interacting;
		m_data.m_progress = m_cleaningTimer;
		SendServerEvent(m_data);
	}

	private void SendAddPlates()
	{
		m_data.m_msgType = WashingStationMessage.MessageType.AddPlates;
		m_data.m_plateCount = m_plateCount;
		SendServerEvent(m_data);
	}

	private void SendCleanedPlate()
	{
		m_data.m_msgType = WashingStationMessage.MessageType.CleanedPlate;
		m_data.m_plateCount = m_plateCount;
		SendServerEvent(m_data);
	}

	private void OnInteracterAdded(GameObject _interacter, Vector2 _directionXZ)
	{
		m_interactor = _interacter;
		SynchroniseInteractionState(m_interactable.IsBeingInteractedWith());
	}

	private void OnInteracterRemoved(GameObject _interacter)
	{
		SynchroniseInteractionState(m_interactable.IsBeingInteractedWith());
	}

	public override void UpdateSynchronising()
	{
		if (!(m_interactable != null) || !m_interactable.IsBeingInteractedWith() || m_plateCount <= 0)
		{
			return;
		}
		m_cleaningTimer += TimeManager.GetDeltaTime(base.gameObject) * ((!m_interactable.AllowMultipleInteractors()) ? 1f : ((float)m_interactable.InteractorCount()));
		if (m_cleaningTimer > m_washingStation.m_cleanPlateTime)
		{
			m_cleaningTimer = 0f;
			m_plateCount--;
			ServerMessenger.Achievement(m_interactor.gameObject, 2);
			SendCleanedPlate();
			m_plateReturnStation.ReturnPlate();
			if (m_plateCount == 0)
			{
				m_interactable.enabled = false;
			}
		}
	}

	private void OnItemAdded(IAttachment _attachment)
	{
		if (m_handlePickupReferral != null)
		{
			m_handlePickupReferral.SetHandlePickupReferree(null);
		}
		GameObject gameObject = (_attachment as MonoBehaviour).gameObject;
		ServerStack component = gameObject.GetComponent<ServerStack>();
		if (component != null)
		{
			m_serverAttachStation.TakeItem();
			NetworkUtils.DestroyObjectsRecursive(gameObject);
			AddPlates(component.GetSize());
		}
	}

	private void OnItemRemoved(IAttachment _attachment)
	{
		if (m_handlePickupReferral != null)
		{
			m_handlePickupReferral.SetHandlePickupReferree(m_originalPickupReferee);
		}
	}

	private void OnItemAddedDryingStation(IAttachment _attachment)
	{
		if (m_interactable != null)
		{
			m_interactable.SetInteractionSuppressed(_attachment.AccessGameObject() != null && _attachment.AccessGameObject().RequestComponent<ServerCleanPlateStack>() == null);
		}
	}

	private void OnItemRemovedDryingStation(IAttachment _attachment)
	{
		if (m_interactable != null)
		{
			m_interactable.SetInteractionSuppressed(false);
		}
	}

	private void AddPlates(int plateCount)
	{
		m_plateCount += plateCount;
		if (m_plateCount > 0)
		{
			m_interactable.enabled = true;
		}
		SendAddPlates();
	}

	public void WashAllPlates()
	{
		if (m_plateCount > 0)
		{
			for (int i = 0; i < m_plateCount; i++)
			{
				m_plateReturnStation.ReturnPlate();
			}
			m_plateCount = 0;
			SendCleanedPlate();
			m_cleaningTimer = 0f;
			m_interactable.enabled = false;
		}
	}

	public bool CanHandlePlacement(ICarrier _carrier, Vector2 _directionXZ, PlacementContext _context)
	{
		ServerStack component = _carrier.InspectCarriedItem().GetComponent<ServerStack>();
		return (component == null && m_dryingAttachStation.CanHandlePlacement(_carrier, _directionXZ, _context)) || m_washingStation.CanHandlePlacement(_carrier, _directionXZ, m_plateCount);
	}

	public void HandlePlacement(ICarrier _carrier, Vector2 _directionXZ, PlacementContext _context)
	{
		GameObject gameObject = _carrier.InspectCarriedItem();
		ServerStack component = gameObject.GetComponent<ServerStack>();
		if (component != null)
		{
			_carrier.DestroyCarriedItem();
			AddPlates(component.GetSize());
		}
		else if (m_dryingAttachStation.CanHandlePlacement(_carrier, _directionXZ, _context))
		{
			m_dryingAttachStation.HandlePlacement(_carrier, _directionXZ, _context);
		}
		else if (m_serverAttachStation.CanHandlePlacement(_carrier, _directionXZ, _context))
		{
			m_serverAttachStation.HandlePlacement(_carrier, _directionXZ, _context);
		}
	}

	public void OnFailedToPlace(GameObject _item)
	{
	}

	public int GetPlacementPriority()
	{
		return int.MaxValue;
	}
}
