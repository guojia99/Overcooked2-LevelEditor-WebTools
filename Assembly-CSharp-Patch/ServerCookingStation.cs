using LevelEditor;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerCookingStation : ServerSynchroniserBase
{
	private CookingStation m_cookingStation;

	private CookingStationMessage m_data = new CookingStationMessage();

	private LevelConfigBase m_levelConfigBase;

	private ServerFlammable m_flammable;

	private ICookable m_itemPot;

	private ServerAttachStation m_attachStation;

	protected float m_cookingSpeed = 1f;

	protected bool m_isTurnedOn;

	protected bool m_isCooking;

	private bool m_pendingSyncMessage;

	public CookingStationType StationType
	{
		get
		{
			return m_cookingStation.m_stationType;
		}
	}

	public override EntityType GetEntityType()
	{
		return EntityType.CookingStation;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
	}

	private void SynchroniseCookingState()
	{
		m_data.m_isTurnedOn = m_isTurnedOn;
		m_data.m_isCooking = m_isCooking;
		m_pendingSyncMessage = true;
	}

	private void SetCookerOn(bool _isOn)
	{
		m_isTurnedOn = _isOn;
		SynchroniseCookingState();
	}

	private void SetCooking(bool _isCooking)
	{
		m_isCooking = _isCooking;
		SynchroniseCookingState();
	}

	protected virtual void Awake()
	{
		m_cookingStation = base.gameObject.RequireComponent<CookingStation>();
		m_attachStation = base.gameObject.GetComponent<ServerAttachStation>();
		m_flammable = base.gameObject.GetComponent<ServerFlammable>();
		m_attachStation.RegisterOnItemAdded(OnItemAdded);
		m_attachStation.RegisterOnItemRemoved(OnItemRemoved);
		m_attachStation.RegisterAllowItemPlacement(CanAddItem);
		m_levelConfigBase = GameUtils.GetLevelConfig();
		if (m_attachStation.HasItem())
		{
			OnItemAdded(m_attachStation.InspectItem().GetComponent<IAttachment>());
		}
	}

	public override void OnDestroy()
	{
		if (m_attachStation != null)
		{
			m_attachStation.UnregisterOnItemAdded(OnItemAdded);
			m_attachStation.UnregisterOnItemRemoved(OnItemRemoved);
			m_attachStation.UnregisterAllowItemPlacement(CanAddItem);
		}
		base.OnDestroy();
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
			IMixable mixable = _object.RequestInterface<IMixable>();
			if (mixable != null)
			{
				mixingProgress = mixable.GetMixedOrderState();
			}
			return m_cookingStation.CanAddItem(_object, mixingProgress);
		}
		default:
			return false;
		}
	}

	protected virtual void OnItemAdded(IAttachment _iHoldable)
	{
		m_itemPot = _iHoldable.AccessGameObject().RequestInterface<ICookable>();
		if (m_itemPot != null)
		{
			// patch
			//if (m_itemPot.GetRequiredStationType() == StationType)
			bool flag = m_itemPot.GetRequiredStationType() == StationType;
			Component component = m_itemPot as Component;
			if (component != null)
			{
				MultiCookingStationTypes multiCookingStationTypes = component.GetComponent<MultiCookingStationTypes>();
				if (multiCookingStationTypes != null && multiCookingStationTypes.MatchType(StationType))
					flag = true;
			}
            if (flag)
			// patch
			{
				IOrderDefinition orderDefinition = _iHoldable.AccessGameObject().RequireInterface<IOrderDefinition>();
				orderDefinition.RegisterOrderCompositionChangedCallback(OnOrderCompositionChanged);
				OnOrderCompositionChanged(orderDefinition.GetOrderComposition());
			}
			if (m_cookingStation.m_itemBlock != null)
			{
				m_cookingStation.m_itemBlock.enabled = false;
			}
		}
	}

	protected virtual void OnItemRemoved(IAttachment _iHoldable)
	{
		if (m_itemPot != null)
		{
			GameObject obj = (m_itemPot as MonoBehaviour).gameObject;
            // patch
            //if (m_itemPot.GetRequiredStationType() == StationType)
            bool flag = m_itemPot.GetRequiredStationType() == StationType;
            Component component = m_itemPot as Component;
            if (component != null)
            {
                MultiCookingStationTypes multiCookingStationTypes = component.GetComponent<MultiCookingStationTypes>();
                if (multiCookingStationTypes != null && multiCookingStationTypes.MatchType(StationType))
                    flag = true;
            }
            if (flag)
			// patch
			{
				IOrderDefinition orderDefinition = obj.RequireInterface<IOrderDefinition>();
				orderDefinition.UnregisterOrderCompositionChangedCallback(OnOrderCompositionChanged);
			}
			OnOrderCompositionChanged(AssembledDefinitionNode.NullNode);
			m_itemPot = null;
			if (m_cookingStation.m_itemBlock != null)
			{
				m_cookingStation.m_itemBlock.enabled = true;
			}
		}
	}

	private bool CanCook(AssembledDefinitionNode _contents)
	{
		if (m_flammable != null && m_flammable.OnFire())
		{
			return false;
		}
		AssembledDefinitionNode assembledDefinitionNode = _contents.Simpilfy();
		if (assembledDefinitionNode == AssembledDefinitionNode.NullNode)
		{
			return false;
		}
		if (assembledDefinitionNode is CompositeAssembledNode)
		{
			CompositeAssembledNode compositeAssembledNode = _contents as CompositeAssembledNode;
			for (int i = 0; i < compositeAssembledNode.m_composition.Length; i++)
			{
				MixedCompositeAssembledNode mixedCompositeAssembledNode = compositeAssembledNode.m_composition[i] as MixedCompositeAssembledNode;
				if (mixedCompositeAssembledNode != null && mixedCompositeAssembledNode.m_progress != MixedCompositeOrderNode.MixingProgress.Mixed)
				{
					return false;
				}
			}
		}
		else
		{
			MixedCompositeAssembledNode mixedCompositeAssembledNode2 = _contents as MixedCompositeAssembledNode;
			if (mixedCompositeAssembledNode2 != null && mixedCompositeAssembledNode2.m_progress != MixedCompositeOrderNode.MixingProgress.Mixed)
			{
				return false;
			}
		}
		if (m_itemPot != null && (_contents is MixedCompositeAssembledNode || assembledDefinitionNode is MixedCompositeAssembledNode))
		{
			CookedCompositeAssembledNode cookedCompositeAssembledNode = assembledDefinitionNode as CookedCompositeAssembledNode;
			if (cookedCompositeAssembledNode == null)
			{
				cookedCompositeAssembledNode = new CookedCompositeAssembledNode();
				cookedCompositeAssembledNode.m_composition = new AssembledDefinitionNode[1] { assembledDefinitionNode };
				cookedCompositeAssembledNode.m_cookingStep = m_itemPot.AccessCookingType;
			}
			cookedCompositeAssembledNode.m_progress = CookedCompositeOrderNode.CookingProgress.Cooked;
			if (!GameUtils.IsValidRecipe(cookedCompositeAssembledNode))
			{
				return false;
			}
		}
		return true;
	}

	private void OnOrderCompositionChanged(AssembledDefinitionNode _contents)
	{
		SetCookerOn(CanCook(_contents));
	}

	public override void UpdateSynchronising()
	{
		if (m_isTurnedOn && m_itemPot != null)
		{
			bool flag = m_itemPot.Cook(m_cookingSpeed * TimeManager.GetDeltaTime(base.gameObject));
			if (m_isCooking && m_levelConfigBase != null && m_levelConfigBase.m_hazardInfo != null && m_levelConfigBase.m_hazardInfo.FireConfigData != null && m_itemPot.IsBurning())
			{
				SetCookerOn(false);
				if (m_flammable != null)
				{
					m_flammable.Ignite();
				}
			}
			if (flag != m_isCooking)
			{
				SetCooking(flag);
			}
		}
		else if (m_isCooking)
		{
			SetCooking(false);
		}
		if (m_pendingSyncMessage)
		{
			m_pendingSyncMessage = false;
			SendServerEvent(m_data);
		}
	}
}
