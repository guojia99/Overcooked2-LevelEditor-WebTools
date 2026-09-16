using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientWashingStation : ClientSynchroniserBase, IClientHandlePlacement, IBaseHandlePlacement
{
	private WashingStation m_washingStation;

	private ClientAttachStation m_clientAttachStation;

	private ClientAttachStation m_dryingAttachStation;

	private ClientHandlePickupReferral m_handlePickupreferral;

	private ClientInteractable m_interactable;

	private IClientHandlePickup m_pickupReferree;

	private int m_plateCount;

	private bool m_isWashing;

	private ProgressUIController m_progressUI;

	private float m_cleaningTimer;

	public override EntityType GetEntityType()
	{
		return EntityType.WashingStation;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		if (m_washingStation == null)
		{
			m_washingStation = (WashingStation)synchronisedObject;
		}
		m_interactable = base.gameObject.GetComponent<ClientInteractable>();
		m_interactable.enabled = false;
		m_interactable.SetStickyInteractionCallback(() => true);
		m_clientAttachStation = base.gameObject.RequireComponent<ClientAttachStation>();
		m_handlePickupreferral = base.gameObject.RequireComponent<ClientHandlePickupReferral>();
		m_clientAttachStation.RegisterOnItemAdded(OnItemAddedOntoSink);
		m_clientAttachStation.RegisterOnItemRemoved(OnItemRemovedFromSink);
		m_pickupReferree = m_handlePickupreferral.GetHandlePickupReferree();
		m_dryingAttachStation = m_washingStation.m_dryingStation.gameObject.RequireComponent<ClientAttachStation>();
		m_dryingAttachStation.RegisterOnItemAdded(OnItemAddedDryingStation);
		m_dryingAttachStation.RegisterOnItemRemoved(OnItemRemovedDryingStation);
	}

	public override void ApplyServerEvent(Serialisable serialisable)
	{
		WashingStationMessage washingStationMessage = (WashingStationMessage)serialisable;
		switch (washingStationMessage.m_msgType)
		{
		case WashingStationMessage.MessageType.InteractionState:
			m_isWashing = washingStationMessage.m_interacting;
			m_cleaningTimer = washingStationMessage.m_progress;
			if (m_isWashing)
			{
				m_progressUI.SetVisibility(true);
			}
			else
			{
				m_progressUI.SetProgress(Mathf.Clamp01(m_cleaningTimer / m_washingStation.m_cleanPlateTime));
			}
			break;
		case WashingStationMessage.MessageType.AddPlates:
			m_plateCount = washingStationMessage.m_plateCount;
			m_interactable.enabled = true;
			OnPlatesAdded();
			break;
		case WashingStationMessage.MessageType.CleanedPlate:
			m_plateCount = washingStationMessage.m_plateCount;
			if (m_plateCount == 0)
			{
				m_interactable.enabled = false;
			}
			OnPlateCleaned();
			break;
		}
	}

	private void Awake()
	{
		if (m_washingStation == null)
		{
			m_washingStation = base.gameObject.RequireComponent<WashingStation>();
		}
		for (int i = 0; i < m_washingStation.m_dirtyPlates.Length; i++)
		{
			m_washingStation.m_dirtyPlates[i].SetActive(false);
		}
		GameObject gameObject = GameUtils.InstantiateUIController(m_washingStation.m_progressUIPrefab.gameObject, "HoverIconCanvas");
		m_progressUI = gameObject.GetComponent<ProgressUIController>();
		m_progressUI.SetFollowTransform(base.transform, Vector3.zero);
	}

	protected override void OnDestroy()
	{
		if (m_progressUI != null && m_progressUI.gameObject != null)
		{
			Object.Destroy(m_progressUI);
		}
		base.OnDestroy();
	}

	public override void UpdateSynchronising()
	{
		if (m_isWashing && m_plateCount > 0)
		{
			m_cleaningTimer += TimeManager.GetDeltaTime(base.gameObject) * (float)m_interactable.InteractorCount() * ((!m_interactable.AllowMultipleInteractors()) ? 0f : 1f);
			m_progressUI.SetProgress(Mathf.Clamp01(m_cleaningTimer / m_washingStation.m_cleanPlateTime));
		}
	}

	private void OnItemAddedOntoSink(IClientAttachment _iHoldable)
	{
		m_handlePickupreferral.SetHandlePickupReferree(null);
		m_interactable.SetInteractionSuppressed(true);
	}

	private void OnItemRemovedFromSink(IClientAttachment _iHoldable)
	{
		m_handlePickupreferral.SetHandlePickupReferree(m_pickupReferree);
		m_interactable.SetInteractionSuppressed(false);
	}

	private void OnItemAddedDryingStation(IClientAttachment _iHoldable)
	{
		if (m_interactable != null)
		{
			m_interactable.SetInteractionSuppressed(_iHoldable.AccessGameObject() != null && _iHoldable.AccessGameObject().RequestComponent<ClientCleanPlateStack>() == null);
		}
	}

	private void OnItemRemovedDryingStation(IClientAttachment _iHoldable)
	{
		if (m_interactable != null)
		{
			m_interactable.SetInteractionSuppressed(false);
		}
	}

	private void OnPlatesAdded()
	{
		UpdateCosmetics();
	}

	private void OnPlateCleaned()
	{
		if (m_plateCount > 0)
		{
			m_cleaningTimer = 0f;
		}
		else
		{
			m_progressUI.SetVisibility(false);
		}
		GameUtils.TriggerAudio(GameOneShotAudioTag.WashedPlate, base.gameObject.layer);
		UpdateCosmetics();
	}

	private void UpdateCosmetics()
	{
		for (int i = 0; i < m_washingStation.m_dirtyPlates.Length; i++)
		{
			m_washingStation.m_dirtyPlates[i].SetActive(false);
		}
		for (int j = 0; j < Mathf.Min(m_plateCount, m_washingStation.m_dirtyPlates.Length); j++)
		{
			m_washingStation.m_dirtyPlates[j].SetActive(true);
		}
	}

	public bool CanHandlePlacement(ICarrier _carrier, Vector2 _directionXZ, PlacementContext _context)
	{
		ClientStack component = _carrier.InspectCarriedItem().GetComponent<ClientStack>();
		return (component == null && m_dryingAttachStation.CanHandlePlacement(_carrier, _directionXZ, _context)) || m_washingStation.CanHandlePlacement(_carrier, _directionXZ, m_plateCount);
	}

	public int GetPlacementPriority()
	{
		return int.MaxValue;
	}
}
