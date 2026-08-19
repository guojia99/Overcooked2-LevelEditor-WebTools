using LevelEditor;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientFurnaceOvenCosmeticDecisions : ClientSynchroniserBase
{
	private HeatedCookingStation m_cookingStation;

	private FurnaceOvenCosmeticDecisions m_cosmetics;

	private IClientAttachment m_item;

	private IOrderDefinition m_orderDefinition;

	private ClientAttachStation m_attachStation;

	private ClientHeatedStation m_heatedStation;

	private AudioManager m_audioManager;

	private static int m_Open = Animator.StringToHash("Open");

	private object m_highHeatToken = new object();

	private object m_mediumHeatToken = new object();

	private object m_activeToken;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_cosmetics = (FurnaceOvenCosmeticDecisions)synchronisedObject;
		m_attachStation = base.gameObject.GetComponent<ClientAttachStation>();
		m_attachStation.RegisterOnItemAdded(OnItemAdded);
		m_attachStation.RegisterOnItemRemoved(OnItemRemoved);
		m_cookingStation = base.gameObject.RequireComponent<HeatedCookingStation>();
		if (m_cookingStation.m_heatSource != null)
		{
			m_heatedStation = m_cookingStation.m_heatSource.gameObject.RequireComponent<ClientHeatedStation>();
			m_heatedStation.RegisterHeatRangeChangedCallback(OnHeatRangeChanged);
		}
		UpdateVisuals(HeatRange.Low);
		m_audioManager = GameUtils.RequestManager<AudioManager>();
	}

	protected override void OnDisable()
	{
		base.OnDisable();
		if (m_audioManager != null && m_activeToken != null)
		{
			m_audioManager.StopAudio(GameLoopingAudioTag.COUNT, m_activeToken);
		}
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		if (m_item != null)
		{
			OnItemRemoved(m_item);
		}
		if (m_attachStation != null)
		{
			m_attachStation.UnregisterOnItemAdded(OnItemAdded);
			m_attachStation.UnregisterOnItemRemoved(OnItemRemoved);
		}
		if (m_heatedStation != null)
		{
			m_heatedStation.UnregisterHeatRangeChangedCallback(OnHeatRangeChanged);
		}
	}

	private void OnItemAdded(IClientAttachment _iHoldable)
	{
		m_item = _iHoldable;
		IOrderDefinition orderDefinition = _iHoldable.AccessGameObject().RequestInterface<IOrderDefinition>();
		if (orderDefinition != null)
		{
			orderDefinition.RegisterOrderCompositionChangedCallback(OnOrderCompositionChanged);
		}
		SetOccupiedState(IsOccupantValid(_iHoldable));
	}

	private void OnItemRemoved(IClientAttachment _iHoldable)
	{
		if (m_item != null)
		{
			IOrderDefinition orderDefinition = _iHoldable.AccessGameObject().RequestInterface<IOrderDefinition>();
			if (orderDefinition != null)
			{
				orderDefinition.UnregisterOrderCompositionChangedCallback(OnOrderCompositionChanged);
			}
			m_item = null;
		}
		SetOccupiedState(false);
	}

	private void OnOrderCompositionChanged(AssembledDefinitionNode _contents)
	{
		SetOccupiedState(IsOrderOccupantValid(_contents));
	}

	private bool IsOccupantValid(IClientAttachment _iHoldable)
	{
		IClientCookable clientCookable = _iHoldable.AccessGameObject().RequestInterface<IClientCookable>();
		// patch
		//if (clientCookable == null || clientCookable.GetRequiredStationType() != m_cookingStation.m_stationType)
		//{
		//	return false;
		//}
		if (clientCookable == null) return false;
		if (clientCookable.GetRequiredStationType() != m_cookingStation.m_stationType)
		{
			Component component = clientCookable as Component;
			if (component == null) return false;
			MultiCookingStationTypes multiCookingStationTypes = component.GetComponent<MultiCookingStationTypes>();
			if (multiCookingStationTypes == null || !multiCookingStationTypes.MatchType(m_cookingStation.m_stationType))
				return false;
		}
		// patch
		IClientOrderDefinition clientOrderDefinition = _iHoldable.AccessGameObject().RequireInterface<IClientOrderDefinition>();
		AssembledDefinitionNode orderComposition = clientOrderDefinition.GetOrderComposition();
		if (!IsOrderOccupantValid(orderComposition))
		{
			return false;
		}
		return true;
	}

	private bool IsOrderOccupantValid(AssembledDefinitionNode _contents)
	{
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
		return true;
	}

	private void SetOccupiedState(bool _isOccupied)
	{
		m_cosmetics.m_animator.SetBool(m_Open, !_isOccupied);
	}

	private void OnHeatRangeChanged(HeatRange _heatRange)
	{
		UpdateVisuals(_heatRange);
		UpdateAudio(_heatRange);
	}

	private void UpdateVisuals(HeatRange _heat)
	{
		ToggleEffect(m_cosmetics.m_highEffect, _heat == HeatRange.High);
		ToggleEffect(m_cosmetics.m_mediumEffect, _heat == HeatRange.Moderate);
		ToggleEffect(m_cosmetics.m_lowEffect, _heat == HeatRange.Low);
	}

	private void UpdateAudio(HeatRange _heat)
	{
		if (m_activeToken != null)
		{
			GameUtils.StopAudio(GameLoopingAudioTag.COUNT, m_activeToken);
		}
		switch (_heat)
		{
		case HeatRange.High:
			m_activeToken = m_highHeatToken;
			break;
		case HeatRange.Moderate:
			m_activeToken = m_mediumHeatToken;
			break;
		default:
			m_activeToken = null;
			break;
		}
	}

	private void ToggleEffect(GameObject _effect, bool _turnOn)
	{
		if (_effect != null)
		{
			_effect.SetActive(_turnOn);
		}
	}
}
