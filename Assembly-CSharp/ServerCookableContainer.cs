using System;
using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerCookableContainer : ServerSynchroniserBase, IOrderDefinition, IContainerTransferBehaviour
{
	private CookableContainer m_cookableContainer;

	private ServerPlacementContainer m_placementContainer;

	private ServerIngredientCatcher m_ingredientCatcher;

	private OrderCompositionChangedCallback m_orderCompositionChangedCallbacks = delegate
	{
	};

	private float m_hideAfterTime = float.MinValue;

	private ServerIngredientContainer m_itemContainer;

	private ServerCookingHandler m_cookingHandler;

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_cookableContainer = (CookableContainer)synchronisedObject;
		m_cookingHandler = base.gameObject.RequireComponent<ServerCookingHandler>();
		m_cookingHandler.CookingStateChangedCallback += delegate
		{
			m_orderCompositionChangedCallbacks(GetOrderComposition());
		};
		m_cookingHandler.enabled = false;
		m_itemContainer = base.gameObject.GetComponent<ServerIngredientContainer>();
		m_itemContainer.RegisterContentsChangedCallback(OnContentsChanged);
		m_placementContainer = base.gameObject.RequireComponent<ServerPlacementContainer>();
		m_placementContainer.RegisterAllowItemPlacement(AllowItemPlacement);
		m_ingredientCatcher = base.gameObject.RequestComponent<ServerIngredientCatcher>();
		if (m_ingredientCatcher != null)
		{
			m_ingredientCatcher.RegisterAllowItemCatching(AllowItemCatching);
		}
		Mailbox.Server.RegisterForMessageType(MessageType.GameState, OnGameStateChanged);
	}

	private void OnGameStateChanged(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
	{
		GameStateMessage gameStateMessage = (GameStateMessage)message;
		if (gameStateMessage.m_State == GameState.StartEntities)
		{
			m_cookingHandler.SetCookingProgress(0f);
		}
	}

	public bool CanTransferToContainer(IIngredientContents _container)
	{
		return AssembledNodeTransfer.CanTransferFromContainer(this, _container);
	}

	public void TransferToContainer(ICarrierPlacement _carrier, IIngredientContents _plateContainer, bool _dontRemove)
	{
		AssembledNodeTransfer.TransferFromContainer(this, _plateContainer, _dontRemove);
	}

	public AssembledDefinitionNode GetOrderComposition()
	{
		ServerMixableContainer serverMixableContainer = base.gameObject.RequestComponent<ServerMixableContainer>();
		AssembledDefinitionNode cookableMixableContents = null;
		bool isMixed = false;
		if (serverMixableContainer != null)
		{
			cookableMixableContents = serverMixableContainer.GetOrderComposition();
			isMixed = serverMixableContainer.GetMixingHandler().IsMixed();
		}
		return m_cookableContainer.GetOrderComposition(m_itemContainer, m_cookingHandler, cookableMixableContents, isMixed);
	}

	public void RegisterOrderCompositionChangedCallback(OrderCompositionChangedCallback _callback)
	{
		m_orderCompositionChangedCallbacks = (OrderCompositionChangedCallback)Delegate.Combine(m_orderCompositionChangedCallbacks, _callback);
	}

	public void UnregisterOrderCompositionChangedCallback(OrderCompositionChangedCallback _callback)
	{
		m_orderCompositionChangedCallbacks = (OrderCompositionChangedCallback)Delegate.Remove(m_orderCompositionChangedCallbacks, _callback);
	}

	private bool AllowItemPlacement(GameObject _object, PlacementContext _context)
	{
		return m_cookableContainer.AllowItemPlacement(_object, _context, m_cookingHandler);
	}

	private bool AllowItemCatching(GameObject _object)
	{
		return AllowItemPlacement(_object, default(PlacementContext));
	}

	public bool CanTransferPrecookedContents(AssembledDefinitionNode[] _contents, float _normalisedCookedProgress)
	{
		if (!m_itemContainer.CanTakeContents(_contents))
		{
			return false;
		}
		return true;
	}

	public void TransferPrecookedContents(AssembledDefinitionNode[] _contents, float _normalisedCookedProgress)
	{
		for (int i = 0; i < _contents.Length; i++)
		{
		}
		float receivedProgress = _normalisedCookedProgress * m_cookingHandler.AccessCookingTime;
		float cookingProgress = m_cookingHandler.GetCookingProgress();
		cookingProgress = CalculateCombinedCookingProgress(cookingProgress, m_itemContainer.GetContentsCount(), receivedProgress, _contents.Length);
		m_cookingHandler.SetCookingProgress(cookingProgress);
		m_itemContainer.CopyContents(_contents);
		GameUtils.TriggerAudio(GameOneShotAudioTag.AddToPot, base.gameObject.layer);
	}

	private float CalculateCombinedCookingProgress(float recipientProgress, int recipientContents, float receivedProgress, int receivedContents)
	{
		if (Mathf.Max(recipientProgress, receivedProgress) > 2f * m_cookingHandler.AccessCookingTime)
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
		return (Mathf.Min(recipientProgress, m_cookingHandler.AccessCookingTime) + Mathf.Min(receivedProgress, m_cookingHandler.AccessCookingTime)) * 0.5f;
	}

	public int GetPlacementPriority()
	{
		return 0;
	}

	public void OnContentsChanged(AssembledDefinitionNode[] _contents)
	{
		m_orderCompositionChangedCallbacks(GetOrderComposition());
		m_cookingHandler.enabled = _contents.Length > 0;
		m_cookingHandler.SetCookingProgress((_contents.Length <= 0) ? 0f : m_cookingHandler.GetCookingProgress());
	}

	public override void OnDestroy()
	{
		base.OnDestroy();
		if (null != m_placementContainer)
		{
			m_placementContainer.UnregisterAllowItemPlacement(AllowItemPlacement);
		}
		Mailbox.Server.UnregisterForMessageType(MessageType.GameState, OnGameStateChanged);
	}

	public ServerCookingHandler GetCookingHandler()
	{
		return m_cookingHandler;
	}
}
