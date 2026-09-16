using System;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerMixableContainer : ServerSynchroniserBase, IOrderDefinition, IContainerTransferBehaviour
{
	private MixableContainer m_mixableContainer;

	private ServerMixingHandler m_MixingHandler;

	private ServerIngredientContainer m_IngredientContainer;

	private ServerPlacementContainer m_PlacementContainer;

	private ServerIngredientCatcher m_IngredientCatcher;

	private OrderCompositionChangedCallback m_orderCompositionChangedCallbacks = delegate
	{
	};

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_mixableContainer = (MixableContainer)synchronisedObject;
		m_MixingHandler = base.gameObject.RequireComponent<ServerMixingHandler>();
		ServerMixingHandler mixingHandler = m_MixingHandler;
		mixingHandler.m_stateChangedCallback = (StateChanged)Delegate.Combine(mixingHandler.m_stateChangedCallback, (StateChanged)delegate
		{
			m_orderCompositionChangedCallbacks(GetOrderComposition());
		});
		m_MixingHandler.enabled = false;
		m_PlacementContainer = base.gameObject.RequireComponent<ServerPlacementContainer>();
		m_PlacementContainer.RegisterAllowItemPlacement(AllowItemPlacement);
		m_IngredientContainer = base.gameObject.GetComponent<ServerIngredientContainer>();
		m_IngredientContainer.RegisterContentsChangedCallback(OnContentsChanged);
		m_IngredientCatcher = base.gameObject.RequestComponent<ServerIngredientCatcher>();
		if (m_IngredientCatcher != null)
		{
			m_IngredientCatcher.RegisterAllowItemCatching(AllowItemCatching);
		}
	}

	public bool CanTransferPremixedContents(AssembledDefinitionNode[] _contents, float _normalisedMixingProgress)
	{
		return m_IngredientContainer.CanTakeContents(_contents);
	}

	public void TransferPremixedContents(AssembledDefinitionNode[] _contents, float _normalisedMixingProgress)
	{
		for (int i = 0; i < _contents.Length; i++)
		{
		}
		float receivedProgress = _normalisedMixingProgress * m_MixingHandler.AccessMixingTime;
		float mixingProgress = m_MixingHandler.GetMixingProgress();
		mixingProgress = CalculateCombinedMixingProgress(mixingProgress, m_IngredientContainer.GetContentsCount(), receivedProgress, _contents.Length);
		m_MixingHandler.SetMixingProgress(mixingProgress);
		m_IngredientContainer.CopyContents(_contents);
		GameUtils.TriggerAudio(GameOneShotAudioTag.AddToPot, base.gameObject.layer);
	}

	private float CalculateCombinedMixingProgress(float recipientProgress, int recipientContents, float receivedProgress, int receivedContents)
	{
		if (Mathf.Max(recipientProgress, receivedProgress) > 2f * m_MixingHandler.AccessMixingTime)
		{
			return Mathf.Max(recipientProgress, receivedProgress);
		}
		if (recipientContents == 0)
		{
			return receivedProgress;
		}
		if (receivedContents == 0)
		{
			return recipientProgress;
		}
		return (Mathf.Min(recipientProgress, m_MixingHandler.AccessMixingTime) + Mathf.Min(receivedProgress, m_MixingHandler.AccessMixingTime)) * 0.5f;
	}

	private bool AllowItemPlacement(GameObject _object, PlacementContext _context)
	{
		float cookingProgress = 0f;
		ServerCookableContainer serverCookableContainer = base.gameObject.RequestComponent<ServerCookableContainer>();
		if (serverCookableContainer != null)
		{
			ServerCookingHandler cookingHandler = serverCookableContainer.GetCookingHandler();
			if (cookingHandler != null)
			{
				cookingProgress = cookingHandler.GetCookingProgress();
			}
		}
		return m_mixableContainer.AllowItemPlacement(_object, _context, m_mixableContainer.m_ApprovedIngredients, m_MixingHandler.IsOverMixed(), cookingProgress);
	}

	private bool AllowItemCatching(GameObject _object)
	{
		return AllowItemPlacement(_object, default(PlacementContext));
	}

	public void OnContentsChanged(AssembledDefinitionNode[] _contents)
	{
		m_orderCompositionChangedCallbacks(GetOrderComposition());
		m_MixingHandler.enabled = _contents.Length > 0;
		m_MixingHandler.SetMixingProgress((_contents.Length <= 0) ? 0f : m_MixingHandler.GetMixingProgress());
	}

	public AssembledDefinitionNode GetOrderComposition()
	{
		return m_mixableContainer.GetOrderComposition(m_IngredientContainer, m_MixingHandler.GetMixingProgress() / m_MixingHandler.AccessMixingTime, m_MixingHandler.GetMixedOrderState());
	}

	public void RegisterOrderCompositionChangedCallback(OrderCompositionChangedCallback _callback)
	{
		m_orderCompositionChangedCallbacks = (OrderCompositionChangedCallback)Delegate.Combine(m_orderCompositionChangedCallbacks, _callback);
	}

	public void UnregisterOrderCompositionChangedCallback(OrderCompositionChangedCallback _callback)
	{
		m_orderCompositionChangedCallbacks = (OrderCompositionChangedCallback)Delegate.Remove(m_orderCompositionChangedCallbacks, _callback);
	}

	public void TransferToContainer(ICarrierPlacement _carrier, IIngredientContents _plateContainer, bool _dontRemove)
	{
		AssembledNodeTransfer.TransferFromContainer(this, _plateContainer, _dontRemove);
	}

	public bool CanTransferToContainer(IIngredientContents _container)
	{
		return AssembledNodeTransfer.CanTransferFromContainer(this, _container);
	}

	public override void OnDestroy()
	{
		base.OnDestroy();
		if (m_PlacementContainer != null)
		{
			m_PlacementContainer.UnregisterAllowItemPlacement(AllowItemPlacement);
		}
	}

	public ServerMixingHandler GetMixingHandler()
	{
		return m_MixingHandler;
	}
}
