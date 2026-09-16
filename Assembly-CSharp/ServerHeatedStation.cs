using System;
using System.Collections.Generic;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerHeatedStation : ServerSynchroniserBase, IHeatContainer, IHandlePlacement, IHandleCatch, IBaseHandlePlacement
{
	public class HolderAdapter : ICarrier, ICarrierPlacement
	{
		private GameObject m_carriedItem;

		public HolderAdapter(GameObject _object)
		{
			m_carriedItem = _object;
		}

		public void RegisterCarriedItemChangeCallback(VoidGeneric<GameObject, GameObject> _callback)
		{
		}

		public void UnregisterCarriedItemChangeCallback(VoidGeneric<GameObject, GameObject> _callback)
		{
		}

		public GameObject InspectCarriedItem()
		{
			return m_carriedItem;
		}

		public GameObject AccessGameObject()
		{
			return null;
		}

		public void CarryItem(GameObject _object)
		{
		}

		public GameObject TakeItem()
		{
			GameObject carriedItem = m_carriedItem;
			m_carriedItem = null;
			return carriedItem;
		}

		public void DestroyCarriedItem()
		{
			ServerPlayerRespawnManager.KillOrRespawn(m_carriedItem, null);
			m_carriedItem = null;
		}
	}

	private HeatedStation m_heatedStation;

	private HeatedStationMessage m_data = new HeatedStationMessage();

	private static List<ServerHeatedStation> s_allHeatedStations = new List<ServerHeatedStation>();

	private ServerAttachStation m_attachStation;

	private float m_heatValue;

	private HeatRange m_heatRange = HeatRange.Low;

	private GenericVoid<HeatRange> m_heatRangeChanged = delegate
	{
	};

	public override EntityType GetEntityType()
	{
		return EntityType.HeatedStation;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_heatedStation = (HeatedStation)synchronisedObject;
		m_attachStation = base.gameObject.RequestComponent<ServerAttachStation>();
	}

	private void SynchroniseHeat()
	{
		m_data.m_msgType = HeatedStationMessage.MsgType.Heat;
		m_data.m_heat = m_heatValue;
		SendServerEvent(m_data);
	}

	private void SynchroniseItemAdded()
	{
		m_data.m_msgType = HeatedStationMessage.MsgType.ItemAdded;
		SendServerEvent(m_data);
	}

	public static List<ServerHeatedStation> GetAllHeatedStations()
	{
		return s_allHeatedStations;
	}

	public void RegisterHeatRangeChangedCallback(GenericVoid<HeatRange> _callback)
	{
		m_heatRangeChanged = (GenericVoid<HeatRange>)Delegate.Combine(m_heatRangeChanged, _callback);
	}

	public void UnregisterHeatRangeChangedCallback(GenericVoid<HeatRange> _callback)
	{
		m_heatRangeChanged = (GenericVoid<HeatRange>)Delegate.Remove(m_heatRangeChanged, _callback);
	}

	protected override void OnEnable()
	{
		base.OnEnable();
		s_allHeatedStations.Add(this);
	}

	protected override void OnDisable()
	{
		base.OnDisable();
		s_allHeatedStations.Remove(this);
	}

	public override void UpdateSynchronising()
	{
		base.UpdateSynchronising();
		if (m_heatValue > 0f && m_heatedStation.m_dissipationRate > 0f)
		{
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
		m_heatValue += _value;
		m_heatValue = Mathf.Clamp01(m_heatValue);
		SynchroniseHeat();
	}

	public void ExternalHeatTransfer(IHeatTransferBehaviour _transferBehaviour)
	{
		if (_transferBehaviour != null && _transferBehaviour.CanTransferToContainer(this))
		{
			ICarrier carrier = new HolderAdapter((_transferBehaviour as MonoBehaviour).gameObject);
			_transferBehaviour.TransferToContainer(carrier, this);
		}
	}

	private void BurnAchievement(GameObject _player, GameObject _object)
	{
		IOrderDefinition orderDefinition = _object.RequestInterface<IOrderDefinition>();
		if (orderDefinition == null || orderDefinition.GetOrderComposition() == AssembledDefinitionNode.NullNode)
		{
			return;
		}
		ItemAssembledNode itemAssembledNode = orderDefinition.GetOrderComposition() as ItemAssembledNode;
		if (itemAssembledNode != null && m_heatedStation.m_burnAchievementFilter != null && m_heatedStation.m_burnAchievementFilter.m_ids.Contains(itemAssembledNode.m_itemOrderNode.m_uID))
		{
			ServerMessenger.Achievement(_player, 501);
		}
		CompositeAssembledNode compositeAssembledNode = orderDefinition.GetOrderComposition() as CompositeAssembledNode;
		if (compositeAssembledNode == null)
		{
			return;
		}
		for (int i = 0; i < compositeAssembledNode.m_composition.Length; i++)
		{
			ItemAssembledNode itemAssembledNode2 = compositeAssembledNode.m_composition[i] as ItemAssembledNode;
			if (itemAssembledNode2 != null && itemAssembledNode2.m_itemOrderNode.m_uID == m_heatedStation.m_coalOrderNode.m_uID)
			{
				ServerMessenger.Achievement(_player, 701);
				break;
			}
		}
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

	public void HandlePlacement(ICarrier _carrier, Vector2 _directionXZ, PlacementContext _context)
	{
		IHeatTransferBehaviour heatTransferBehaviour = _carrier.InspectCarriedItem().RequestInterface<IHeatTransferBehaviour>();
		if (heatTransferBehaviour != null)
		{
			BurnAchievement(_carrier.AccessGameObject(), _carrier.InspectCarriedItem());
			heatTransferBehaviour.TransferToContainer(_carrier, this);
			SynchroniseItemAdded();
		}
		else if (m_attachStation != null && m_attachStation.CanHandlePlacement(_carrier, _directionXZ, _context))
		{
			m_attachStation.HandlePlacement(_carrier, _directionXZ, _context);
		}
	}

	public void OnFailedToPlace(GameObject _item)
	{
	}

	public int GetPlacementPriority()
	{
		return int.MaxValue;
	}

	public bool CanHandleCatch(ICatchable _object, Vector2 _directionXZ)
	{
		if (!_object.AllowCatch(this, _directionXZ))
		{
			return false;
		}
		IHeatTransferBehaviour heatTransferBehaviour = _object.AccessGameObject().RequestInterface<IHeatTransferBehaviour>();
		if (heatTransferBehaviour != null && heatTransferBehaviour.CanTransferToContainer(this))
		{
			return true;
		}
		return m_attachStation != null && m_attachStation.CanHandleCatch(_object, _directionXZ);
	}

	public void HandleCatch(ICatchable _object, Vector2 _directionXZ)
	{
		GameObject gameObject = _object.AccessGameObject();
		IHeatTransferBehaviour heatTransferBehaviour = gameObject.RequestInterface<IHeatTransferBehaviour>();
		if (heatTransferBehaviour != null)
		{
			IThrowable throwable = gameObject.RequireInterface<IThrowable>();
			IThrower thrower = throwable.GetThrower();
			if (thrower != null)
			{
				BurnAchievement((thrower as MonoBehaviour).gameObject, gameObject);
			}
			ICarrier carrier = new HolderAdapter(gameObject);
			heatTransferBehaviour.TransferToContainer(carrier, this);
			SynchroniseItemAdded();
		}
		else if (m_attachStation != null && m_attachStation.CanHandleCatch(_object, _directionXZ))
		{
			m_attachStation.HandleCatch(_object, _directionXZ);
		}
	}

	public void AlertToThrownItem(ICatchable _thrown, IThrower _thrower, Vector2 _directionXZ)
	{
	}

	public int GetCatchingPriority()
	{
		return int.MaxValue;
	}
}
