using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerMixingStation : ServerSynchroniserBase
{
	private MixingStation m_MixingStation;

	protected MixingStationMessage m_data = new MixingStationMessage();

	private ServerAttachStation m_AttachStation;

	private ServerFlammable m_Flammable;

	private LevelConfigBase m_LevelConfig;

	private IMixable m_ItemMixer;

	private bool m_bMixerOn;

	private bool m_pendingSyncMessage;

	public override EntityType GetEntityType()
	{
		return EntityType.MixingStation;
	}

	private void SynchroniseMixingState()
	{
		m_data.m_isTurnedOn = m_bMixerOn;
		m_pendingSyncMessage = true;
	}

	private void Awake()
	{
		m_MixingStation = base.gameObject.RequireComponent<MixingStation>();
		m_AttachStation = base.gameObject.RequireComponent<ServerAttachStation>();
		m_Flammable = base.gameObject.RequireComponent<ServerFlammable>();
		m_LevelConfig = GameUtils.GetLevelConfig();
		m_AttachStation.RegisterAllowItemPlacement(CanAddItem);
		m_AttachStation.RegisterOnItemAdded(OnItemAdded);
		m_AttachStation.RegisterOnItemRemoved(OnItemRemoved);
		if (m_AttachStation.HasItem())
		{
			OnItemAdded(m_AttachStation.InspectItem().RequireInterface<IAttachment>());
		}
	}

	public override void OnDestroy()
	{
		m_AttachStation.UnregisterAllowItemPlacement(CanAddItem);
		m_AttachStation.UnregisterOnItemAdded(OnItemAdded);
		m_AttachStation.UnregisterOnItemRemoved(OnItemRemoved);
		base.OnDestroy();
	}

	public override void UpdateSynchronising()
	{
		if (m_bMixerOn && m_ItemMixer != null && m_ItemMixer.Mix(TimeManager.GetDeltaTime(base.gameObject)) && m_ItemMixer.IsOverMixed())
		{
			SetMixerOn(false);
		}
		if (m_pendingSyncMessage)
		{
			m_pendingSyncMessage = false;
			SendServerEvent(m_data);
		}
	}

	private void SetMixerOn(bool _bOn)
	{
		m_bMixerOn = _bOn;
		SynchroniseMixingState();
	}

	private bool CanAddItem(GameObject _object, PlacementContext _context)
	{
		switch (_context.m_source)
		{
		case PlacementContext.Source.Game:
			return true;
		case PlacementContext.Source.Player:
		{
			if (!m_MixingStation.m_bAttachRestrictions)
			{
				return true;
			}
			IMixable mixable = _object.RequestInterface<IMixable>();
			if (mixable == null)
			{
				return false;
			}
			ICookable cookable = _object.RequestInterface<ICookable>();
			if (cookable != null)
			{
				IIngredientContents ingredientContents = _object.RequestInterface<IIngredientContents>();
				if (ingredientContents != null && ingredientContents.HasContents() && (cookable.GetCookedOrderState() != CookedCompositeOrderNode.CookingProgress.Raw || cookable.GetCookingProgress() > 0f))
				{
					return false;
				}
			}
			return true;
		}
		default:
			return false;
		}
	}

	private void OnItemAdded(IAttachment _iHoldable)
	{
		m_ItemMixer = _iHoldable.AccessGameObject().RequestInterface<IMixable>();
		if (m_ItemMixer != null)
		{
			ServerMixableContainer serverMixableContainer = _iHoldable.AccessGameObject().RequireComponent<ServerMixableContainer>();
			serverMixableContainer.RegisterOrderCompositionChangedCallback(OnOrderCompositionChanged);
			OnOrderCompositionChanged(serverMixableContainer.GetOrderComposition());
			if (m_MixingStation.m_itemBlock != null)
			{
				m_MixingStation.m_itemBlock.enabled = false;
			}
		}
	}

	private void OnItemRemoved(IAttachment _iHoldable)
	{
		if (m_ItemMixer != null)
		{
			GameObject obj = (m_ItemMixer as MonoBehaviour).gameObject;
			ServerMixableContainer serverMixableContainer = obj.RequireComponent<ServerMixableContainer>();
			serverMixableContainer.UnregisterOrderCompositionChangedCallback(OnOrderCompositionChanged);
			OnOrderCompositionChanged(AssembledDefinitionNode.NullNode);
			m_ItemMixer = null;
			if (m_MixingStation.m_itemBlock != null)
			{
				m_MixingStation.m_itemBlock.enabled = true;
			}
		}
	}

	private void OnOrderCompositionChanged(AssembledDefinitionNode _contents)
	{
		if (m_ItemMixer != null)
		{
			SetMixerOn(_contents.Simpilfy() != AssembledDefinitionNode.NullNode && !m_ItemMixer.IsOverMixed() && !m_Flammable.OnFire());
		}
		else
		{
			SetMixerOn(_contents.Simpilfy() != AssembledDefinitionNode.NullNode && !m_Flammable.OnFire());
		}
	}
}
