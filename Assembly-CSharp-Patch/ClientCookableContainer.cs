using System;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientCookableContainer : ClientSynchroniserBase, IClientOrderDefinition
{
	private CookableContainer m_cookableContainer;

	private OrderCompositionChangedCallback m_orderCompositionChangedCallbacks = delegate
	{
	};

	private ClientIngredientContainer m_itemContainer;

	private ClientCookingHandler m_cookingHandler;

	private ClientPlacementContainer m_placementContainer;

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_cookableContainer = (CookableContainer)synchronisedObject;
		m_cookingHandler = base.gameObject.RequireComponent<ClientCookingHandler>();
		m_cookingHandler.CookingStateChangedCallback += delegate(CookingUIController.State _state)
		{
			m_orderCompositionChangedCallbacks(GetOrderComposition());
			if (_state == CookingUIController.State.Ruined)
			{
				OvercookedAchievementManager overcookedAchievementManager = GameUtils.RequestManager<OvercookedAchievementManager>();
				if (overcookedAchievementManager != null)
				{
					overcookedAchievementManager.IncStat(15, 1f, ControlPadInput.PadNum.One);
				}
			}
		};
		m_itemContainer = base.gameObject.GetComponent<ClientIngredientContainer>();
		m_itemContainer.RegisterContentsChangedCallback(OnContentsChanged);
		if (m_cookableContainer.m_cosmeticsPrefab != null)
		{
			Transform parent = NetworkUtils.FindVisualRoot(base.gameObject);
			GameObject gameObject = UnityEngine.Object.Instantiate(m_cookableContainer.m_cosmeticsPrefab, parent);
			if (gameObject != null)
			{
				gameObject.transform.localPosition = Vector3.zero;
				gameObject.transform.localRotation = Quaternion.identity;
                // patch
                gameObject.SetActive(true);
                // patch
            }
        }
		m_placementContainer = base.gameObject.RequireComponent<ClientPlacementContainer>();
		m_placementContainer.RegisterAllowItemPlacement(AllowItemPlacement);
	}

	public AssembledDefinitionNode GetOrderComposition()
	{
		ClientMixableContainer clientMixableContainer = base.gameObject.RequestComponent<ClientMixableContainer>();
		AssembledDefinitionNode cookableMixableContents = null;
		bool isMixed = false;
		if (clientMixableContainer != null)
		{
			cookableMixableContents = clientMixableContainer.GetOrderComposition();
			isMixed = clientMixableContainer.GetMixingHandler().IsMixed();
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

	private void OnContentsChanged(AssembledDefinitionNode[] _contents)
	{
		m_orderCompositionChangedCallbacks(GetOrderComposition());
	}

	private bool AllowItemPlacement(GameObject _object, PlacementContext _context)
	{
		return m_cookableContainer.AllowItemPlacement(_object, _context, m_cookingHandler);
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		if (null != m_placementContainer)
		{
			m_placementContainer.UnregisterAllowItemPlacement(AllowItemPlacement);
		}
	}

	public ClientCookingHandler GetCookingHandler()
	{
		return m_cookingHandler;
	}
}
