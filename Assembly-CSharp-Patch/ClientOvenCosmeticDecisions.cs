using LevelEditor;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientOvenCosmeticDecisions : ClientSynchroniserBase
{
	private CookingStation m_cookingStation;

	private OvenCosmeticDecisions m_ovenCosmeticDecisions;

	private IClientAttachment m_item;

	private IOrderDefinition m_orderDefinition;

	private ClientAttachStation m_attachStation;

	private static int m_Open = Animator.StringToHash("Open");

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_cookingStation = base.gameObject.RequireComponent<CookingStation>();
		m_ovenCosmeticDecisions = base.gameObject.RequireComponent<OvenCosmeticDecisions>();
		m_attachStation = base.gameObject.GetComponent<ClientAttachStation>();
		m_attachStation.RegisterOnItemAdded(OnItemAdded);
		m_attachStation.RegisterOnItemRemoved(OnItemRemoved);
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
		m_ovenCosmeticDecisions.m_animator.SetBool(m_Open, !_isOccupied);
	}
}
