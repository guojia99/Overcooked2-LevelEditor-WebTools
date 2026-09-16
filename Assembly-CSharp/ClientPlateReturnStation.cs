using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientPlateReturnStation : ClientSynchroniserBase
{
	private PlateReturnStation m_returnStation;

	private ClientAttachStation m_attachStation;

	private ClientPlateStackBase m_stack;

	private ClientHandlePlacementReferral m_handlePlacementreferral;

	private IClientHandlePlacement m_placementReferree;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_returnStation = (PlateReturnStation)synchronisedObject;
		NetworkUtils.RegisterSpawnablePrefab(base.gameObject, m_returnStation.m_stackPrefab);
		m_attachStation = GetComponent<ClientAttachStation>();
		m_attachStation.RegisterAllowItemPlacement(CanAddItem);
		m_attachStation.RegisterAllowItemPickup(CanRemoveItem);
		m_attachStation.RegisterOnItemAdded(OnItemAdded);
		m_attachStation.RegisterOnItemRemoved(OnItemRemoved);
		m_handlePlacementreferral = base.gameObject.RequestComponent<ClientHandlePlacementReferral>();
		Mailbox.Client.RegisterForMessageType(MessageType.GameState, OnGameStateChanged);
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		if (null != m_attachStation)
		{
			m_attachStation.UnregisterAllowItemPlacement(CanAddItem);
			m_attachStation.UnregisterAllowItemPickup(CanRemoveItem);
			m_attachStation.UnregisterOnItemAdded(OnItemAdded);
			m_attachStation.UnregisterOnItemRemoved(OnItemRemoved);
			if (m_stack != null)
			{
				m_stack.UnregisterOnPlateAdded(OnPlateAdded);
				m_stack.UnregisterOnPlateRemoved(OnPlatesUpdated);
			}
		}
		Mailbox.Client.UnregisterForMessageType(MessageType.GameState, OnGameStateChanged);
	}

	private void OnGameStateChanged(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
	{
		GameStateMessage gameStateMessage = (GameStateMessage)message;
		if (gameStateMessage.m_State == GameState.StartEntities && m_handlePlacementreferral != null)
		{
			m_placementReferree = m_handlePlacementreferral.GetHandlePlacementReferree();
		}
	}

	public bool HasReturnedPlates()
	{
		return m_stack != null && m_stack.GetCount() > 0;
	}

	public void OnItemAdded(IClientAttachment _iHoldable)
	{
		m_stack = _iHoldable.AccessGameObject().GetComponent<ClientPlateStackBase>();
		if (m_stack != null)
		{
			m_stack.RegisterOnPlateAdded(OnPlateAdded);
			m_stack.RegisterOnPlateRemoved(OnPlatesUpdated);
			GameUtils.TriggerAudio(GameOneShotAudioTag.WashedPlate, base.gameObject.layer);
		}
	}

	public void OnItemRemoved(IClientAttachment _iHoldable)
	{
		if (m_stack != null)
		{
			m_stack.UnregisterOnPlateAdded(OnPlateAdded);
			m_stack.UnregisterOnPlateRemoved(OnPlatesUpdated);
			m_stack = null;
		}
	}

	public void OnPlateAdded(GameObject _plate)
	{
		GameUtils.TriggerAudio(GameOneShotAudioTag.WashedPlate, base.gameObject.layer);
		OnPlatesUpdated(_plate);
	}

	public void OnPlatesUpdated(GameObject _plate)
	{
		if (m_handlePlacementreferral != null)
		{
			m_handlePlacementreferral.SetHandlePlacementReferree((!HasReturnedPlates()) ? m_placementReferree : null);
		}
	}

	private bool CanAddItem(GameObject _object, PlacementContext _context)
	{
		if (_context.m_source == PlacementContext.Source.Player)
		{
			return false;
		}
		if (m_stack != null)
		{
			return false;
		}
		if (m_attachStation.HasItem())
		{
			return false;
		}
		return true;
	}

	private bool CanRemoveItem()
	{
		return m_stack == null || HasReturnedPlates();
	}
}
