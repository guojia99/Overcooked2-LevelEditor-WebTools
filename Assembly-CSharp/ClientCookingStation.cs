using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientCookingStation : ClientSynchroniserBase
{
	private CookingStation m_cookingStation;

	private ClientAttachStation m_attachStation;

	private GameLoopingAudioTag? m_activeLoopingAudio;

	private IClientCookable m_itemPot;

	private bool m_isTurnedOn;

	private bool m_isCooking;

	public override EntityType GetEntityType()
	{
		return EntityType.CookingStation;
	}

	public override void ApplyServerEvent(Serialisable serialisable)
	{
		CookingStationMessage cookingStationMessage = (CookingStationMessage)serialisable;
		SetCookerOn(cookingStationMessage.m_isTurnedOn);
		SetCooking(cookingStationMessage.m_isCooking);
	}

	protected virtual void Awake()
	{
		m_cookingStation = base.gameObject.RequireComponent<CookingStation>();
		m_attachStation = base.gameObject.RequireComponent<ClientAttachStation>();
		m_attachStation.RegisterOnItemAdded(OnItemAdded);
		m_attachStation.RegisterOnItemRemoved(OnItemRemoved);
		m_attachStation.RegisterAllowItemPlacement(CanAddItem);
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		m_attachStation.UnregisterOnItemAdded(OnItemAdded);
		m_attachStation.UnregisterOnItemRemoved(OnItemRemoved);
		m_attachStation.UnregisterAllowItemPlacement(CanAddItem);
	}

	public override void UpdateSynchronising()
	{
		if (m_isTurnedOn)
		{
			if (m_isCooking && !m_activeLoopingAudio.HasValue && m_itemPot != null)
			{
				m_activeLoopingAudio = m_itemPot.GetSizzleSoundTag();
				GameUtils.StartAudio(m_activeLoopingAudio.Value, this, base.gameObject.layer);
			}
		}
		else if (m_activeLoopingAudio.HasValue)
		{
			GameUtils.StopAudio(m_activeLoopingAudio.Value, this);
			m_activeLoopingAudio = null;
		}
	}

	protected override void OnDisable()
	{
		base.OnDisable();
		if (m_activeLoopingAudio.HasValue)
		{
			GameUtils.StopAudio(m_activeLoopingAudio.Value, this);
			m_activeLoopingAudio = null;
		}
	}

	private void SetCookerOn(bool _isOn)
	{
		if (m_cookingStation != null)
		{
			m_isTurnedOn = _isOn;
			m_cookingStation.SetCookerOn(_isOn);
		}
	}

	private void SetCooking(bool _isCooking)
	{
		m_isCooking = _isCooking;
	}

	private void OnItemAdded(IClientAttachment _iHoldable)
	{
		m_itemPot = _iHoldable.AccessGameObject().RequestInterface<IClientCookable>();
		IClientCookingRegionNotified clientCookingRegionNotified = _iHoldable.AccessGameObject().RequestInterfaceRecursive<IClientCookingRegionNotified>();
		if (clientCookingRegionNotified as MonoBehaviour != null)
		{
			clientCookingRegionNotified.EnterCookingRegion();
		}
	}

	private void OnItemRemoved(IClientAttachment _iHoldable)
	{
		SetCookerOn(false);
		SetCooking(false);
		if (m_itemPot != null)
		{
			m_itemPot = null;
		}
		IClientCookingRegionNotified clientCookingRegionNotified = _iHoldable.AccessGameObject().RequestInterfaceRecursive<IClientCookingRegionNotified>();
		if (clientCookingRegionNotified as MonoBehaviour != null)
		{
			clientCookingRegionNotified.ExitCookingRegion();
		}
	}

	private bool CanAddItem(GameObject _object, PlacementContext _context)
	{
		switch (_context.m_source)
		{
		case PlacementContext.Source.Game:
			return true;
		case PlacementContext.Source.Player:
		{
			MixedCompositeOrderNode.MixingProgress? mixingProgress = null;
			IClientMixable clientMixable = _object.RequestInterface<IClientMixable>();
			if (clientMixable != null)
			{
				mixingProgress = clientMixable.GetMixedOrderState();
			}
			return m_cookingStation.CanAddItem(_object, mixingProgress);
		}
		default:
			return false;
		}
	}
}
