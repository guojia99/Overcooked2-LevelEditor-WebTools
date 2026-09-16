using System;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientHeatedStation : ClientSynchroniserBase, IClientHandlePlacement, IHeatContainer, IBaseHandlePlacement
{
	private HeatedStation m_heatedStation;

	private ClientAttachStation m_attachStation;

	private float m_heatValue;

	private HeatRange m_heatRange = HeatRange.Low;

	private GenericVoid<HeatRange> m_heatRangeChanged = delegate
	{
	};

	private CallbackVoid m_onItemAdded = delegate
	{
	};

	public float HeatValue
	{
		get
		{
			return m_heatValue;
		}
	}

	public override EntityType GetEntityType()
	{
		return EntityType.HeatedStation;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_heatedStation = (HeatedStation)synchronisedObject;
		m_attachStation = base.gameObject.RequestComponent<ClientAttachStation>();
	}

	public override void ApplyServerEvent(Serialisable serialisable)
	{
		HeatedStationMessage heatedStationMessage = (HeatedStationMessage)serialisable;
		if (heatedStationMessage.m_msgType == HeatedStationMessage.MsgType.Heat)
		{
			m_heatValue = heatedStationMessage.m_heat;
		}
		else
		{
			m_onItemAdded();
		}
	}

	public void RegisterHeatRangeChangedCallback(GenericVoid<HeatRange> _callback)
	{
		m_heatRangeChanged = (GenericVoid<HeatRange>)Delegate.Combine(m_heatRangeChanged, _callback);
	}

	public void UnregisterHeatRangeChangedCallback(GenericVoid<HeatRange> _callback)
	{
		m_heatRangeChanged = (GenericVoid<HeatRange>)Delegate.Remove(m_heatRangeChanged, _callback);
	}

	public void RegisterOnItemAddedCallback(CallbackVoid _callback)
	{
		m_onItemAdded = (CallbackVoid)Delegate.Combine(m_onItemAdded, _callback);
	}

	public void UnregisterOnItemAddedCallback(CallbackVoid _callback)
	{
		m_onItemAdded = (CallbackVoid)Delegate.Remove(m_onItemAdded, _callback);
	}

	public override void UpdateSynchronising()
	{
		base.UpdateSynchronising();
		if (m_heatValue > 0f && m_heatedStation.m_dissipationRate > 0f)
		{
			float heatValue = m_heatValue;
			m_heatValue -= TimeManager.GetDeltaTime(base.gameObject) / m_heatedStation.m_dissipationRate;
			m_heatValue = Mathf.Max(m_heatValue, 0f);
		}
		HeatRange heat = m_heatedStation.GetHeat(m_heatValue);
		if (heat != m_heatRange)
		{
			m_heatRangeChanged(heat);
		}
		m_heatRange = heat;
	}

	public void IncreaseHeat(float _value)
	{
		throw new NotImplementedException();
	}

	public bool CanHandlePlacement(ICarrier _carrier, Vector2 _directionXZ, PlacementContext _context)
	{
		IHeatTransferBehaviour heatTransferBehaviour = _carrier.InspectCarriedItem().RequestInterface<IHeatTransferBehaviour>();
		if (heatTransferBehaviour != null && heatTransferBehaviour.CanTransferToContainer(this))
		{
			return true;
		}
		return m_attachStation != null && m_attachStation.CanHandlePlacement(_carrier, _directionXZ, _context);
	}

	public int GetPlacementPriority()
	{
		return int.MaxValue;
	}
}
