using System.Collections.Generic;
using OrderController;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public abstract class ClientOrderControllerBase
{
	protected class ActiveOrder
	{
		public OrderID ID;

		public RecipeList.Entry RecipeListEntry;

		public RecipeFlowGUI.ElementToken UIToken;

		public ActiveOrder(OrderID _id, RecipeList.Entry _entry, RecipeFlowGUI.ElementToken _token)
		{
			ID = _id;
			UIToken = _token;
			RecipeListEntry = _entry;
		}
	}

	protected List<ActiveOrder> m_activeOrders = new List<ActiveOrder>();

	private VoidGeneric<RecipeFlowGUI.ElementToken> m_expiredDoNothingCallback = delegate
	{
	};

	protected IClientRoundTimer m_roundTimer;

	protected RecipeFlowGUI m_gui;

	private bool m_enableOrderExpiration = true;

	public bool EnableOrderExpiration
	{
		get
		{
			return m_enableOrderExpiration;
		}
		set
		{
			m_enableOrderExpiration = value;
		}
	}

	public ClientOrderControllerBase(RecipeFlowGUI _flowGUI)
	{
		m_gui = _flowGUI;
		m_gui.gameObject.layer = LayerMask.NameToLayer("Default");
	}

	public virtual void Update()
	{
		float dt = ((m_roundTimer == null || m_roundTimer.IsSuppressed || !m_enableOrderExpiration) ? 0f : TimeManager.GetDeltaTime(m_gui.gameObject.layer));
		m_gui.UpdateTimers(dt);
	}

	public void SetRoundTimer(IClientRoundTimer _timer)
	{
		m_roundTimer = _timer;
	}

	public virtual void AddNewOrder(Serialisable _data)
	{
		ServerOrderData data = (ServerOrderData)_data;
		RecipeList.Entry entry = new RecipeList.Entry();
		entry.Copy(data.RecipeListEntry);
		RecipeFlowGUI.ElementToken token = m_gui.AddElement(entry.m_order, data.Lifetime, m_expiredDoNothingCallback);
		ActiveOrder item = new ActiveOrder(data.ID, entry, token);
		m_activeOrders.Add(item);
	}

	public virtual void OnFoodDelivered(bool _success, OrderID _orderID)
	{
		if (_success)
		{
			ActiveOrder activeOrder = m_activeOrders.Find((ActiveOrder x) => x.ID == _orderID);
			if (activeOrder != null)
			{
				m_gui.RemoveElement(activeOrder.UIToken, new RecipeSuccessAnimation());
			}
			m_activeOrders.RemoveAll((ActiveOrder x) => x.ID == _orderID);
		}
		else
		{
			for (int num = 0; num < m_activeOrders.Count; num++)
			{
				m_gui.PlayAnimationOnElement(m_activeOrders[num].UIToken, new RecipeFailureAnimation());
			}
		}
	}

	public virtual void OnOrderExpired(OrderID _orderID)
	{
		GameUtils.TriggerAudio(GameOneShotAudioTag.RecipeTimeOut, m_gui.gameObject.layer);
		ActiveOrder activeOrder = m_activeOrders.Find((ActiveOrder x) => x.ID == _orderID);
		if (activeOrder != null)
		{
			m_gui.PlayAnimationOnElement(activeOrder.UIToken, new RecipeFailureAnimation());
			m_gui.ResetElementTimer(activeOrder.UIToken);
		}
	}

	public RecipeList.Entry GetRecipe(OrderID _orderID)
	{
		ActiveOrder activeOrder = m_activeOrders.Find((ActiveOrder x) => x.ID == _orderID);
		return activeOrder.RecipeListEntry;
	}
}
