using System.Collections.Generic;
using LevelEditor;
using OrderController;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public abstract class ServerOrderControllerBase
{
	private uint m_nextOrderID = 1u;

	protected List<ServerOrderData> m_activeOrders = new List<ServerOrderData>();

	protected IServerRoundTimer m_roundTimer;

	private RoundDataBase m_roundData;

	private RoundInstanceDataBase m_roundInstanceData;

	private int m_maxOrdersAllowed;

	protected int m_layer;

	protected float m_timerUntilOrder;

	protected bool m_autoProgress = true;

	private int m_comboIndex;

	private bool m_enableOrderExpiration = true;

	private VoidGeneric<OrderID> m_orderAddedCallback = delegate
	{
	};

	protected VoidGeneric<OrderID> m_orderTimeoutCallback = delegate
	{
	};

	public List<RecipeList.Entry> ActiveRecipes
	{
		get
		{
			return m_activeOrders.ConvertAll((ServerOrderData x) => x.RecipeListEntry);
		}
	}

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

	protected RoundInstanceDataBase AccessRoundInstanceData
	{
		get
		{
			return m_roundInstanceData;
		}
	}

	public ServerOrderControllerBase(RoundDataBase _data, int _maxOrders, VoidGeneric<OrderID> _addedCallback, VoidGeneric<OrderID> _timeoutCallback)
	{
		m_roundData = _data;
		m_roundInstanceData = _data.InitialiseRound();
		m_maxOrdersAllowed = _maxOrders;
		m_layer = LayerMask.NameToLayer("Default");
		m_orderAddedCallback = _addedCallback;
		m_orderTimeoutCallback = _timeoutCallback;
	}

	public virtual void Update()
	{
		float num = ((m_roundTimer == null || m_roundTimer.IsSuppressed || !m_enableOrderExpiration) ? 0f : TimeManager.GetDeltaTime(m_layer));
		for (int i = 0; i < m_activeOrders.Count; i++)
		{
			ServerOrderData serverOrderData = m_activeOrders[i];
			if (serverOrderData.Remaining > 0f)
			{
				serverOrderData.Remaining -= num;
				if (serverOrderData.Remaining <= 0f)
				{
					m_orderTimeoutCallback(serverOrderData.ID);
				}
			}
		}
		m_timerUntilOrder -= num;
		// patch
		int minOrderCount = 2;
        if (PseudoPrefabManager.Instance != null)
            minOrderCount = PseudoPrefabManager.Instance.stub.levelInfo.minOrderCount;
        if (m_autoProgress && !IsFull() && (m_timerUntilOrder < 0f || m_activeOrders.Count < minOrderCount))
        // patch
        {
            AddNewOrder();
			m_timerUntilOrder = GetNextTimeBetweenOrders();
		}
	}

	protected void ResetOrderTimer()
	{
		m_timerUntilOrder = 0f;
	}

	protected bool IsEmpty()
	{
		return m_activeOrders.Count == 0;
	}

	protected bool IsFull()
	{
		// patch
		int maxOrderCount = m_maxOrdersAllowed;
		if (PseudoPrefabManager.Instance != null)
			maxOrderCount = PseudoPrefabManager.Instance.stub.levelInfo.maxOrderCount;
        return m_activeOrders.Count >= maxOrderCount;
		//return m_activeOrders.Count >= m_maxOrdersAllowed;
        // patch
    }

    public void SetAutoProgress(bool _autoProgress)
	{
		m_autoProgress = _autoProgress;
	}

	public void SetRoundTimer(IServerRoundTimer _timer)
	{
		m_roundTimer = _timer;
	}

	protected virtual ServerOrderData AddNewOrder(RecipeList.Entry _entry)
	{
		ServerOrderData serverOrderData = new ServerOrderData(new OrderID(m_nextOrderID++), _entry, GetNextOrderLifetime());
		m_activeOrders.Add(serverOrderData);
		m_orderAddedCallback(serverOrderData.ID);
		return serverOrderData;
	}

	public void AddNewOrder()
	{
		RecipeList.Entry[] nextRecipe = m_roundData.GetNextRecipe(m_roundInstanceData);
		for (int i = 0; i < nextRecipe.Length; i++)
		{
			AddNewOrder(nextRecipe[i]);
		}
	}

	public void RemoveOrder(OrderID _orderID)
	{
		int index = m_activeOrders.FindIndex((ServerOrderData x) => x.ID == _orderID);
		m_activeOrders.RemoveAt(index);
	}

	public void ResetOrderLifetime(OrderID _orderID)
	{
		ServerOrderData serverOrderData = m_activeOrders.Find((ServerOrderData x) => x.ID == _orderID);
		serverOrderData.Remaining = serverOrderData.Lifetime;
	}

	protected abstract float GetNextOrderLifetime();

	protected abstract float GetNextTimeBetweenOrders();

	protected bool Matches(OrderDefinitionNode _required, AssembledDefinitionNode _provided, PlatingStepData _plateType)
	{
		if (_required.m_platingStep != _plateType)
		{
			return false;
		}
		if (_required.GetType() == typeof(WildcardOrderNode))
		{
			return AssembledDefinitionNode.Matching(_required, _provided);
		}
		return AssembledDefinitionNode.Matching(_provided, _required);
	}

	public bool FindBestOrderForRecipe(AssembledDefinitionNode _order, PlatingStepData _plateType, out OrderID o_orderID, out float _timePropRemainingPercentage)
	{
		o_orderID = new OrderID(0u);
		_timePropRemainingPercentage = 0f;
		List<ServerOrderData> list = m_activeOrders.FindAll((ServerOrderData x) => Matches(x.RecipeListEntry.m_order, _order, _plateType));
		ServerOrderData value = list.ToArray().FindLowestScoring((ServerOrderData x) => x.Remaining).Value;
		if (value != null)
		{
			o_orderID = value.ID;
			_timePropRemainingPercentage = Mathf.Clamp01(value.Remaining / value.Lifetime);
			return true;
		}
		return false;
	}

	public bool IsComboOrder(OrderID _orderID, bool _restart)
	{
		if (_restart)
		{
			ServerOrderData serverOrderData = m_activeOrders.Find((ServerOrderData x) => x.ID == _orderID);
			if (serverOrderData != null)
			{
				m_comboIndex = m_activeOrders.IndexOf(serverOrderData);
				return m_comboIndex == 0;
			}
			return false;
		}
		if (m_comboIndex >= 0 && m_comboIndex < m_activeOrders.Count)
		{
			ServerOrderData serverOrderData2 = m_activeOrders[m_comboIndex];
			return serverOrderData2.ID == _orderID;
		}
		return false;
	}

	public RecipeList.Entry GetRecipe(OrderID _orderID)
	{
		ServerOrderData serverOrderData = m_activeOrders.Find((ServerOrderData x) => x.ID == _orderID);
		return serverOrderData.RecipeListEntry;
	}

	public Serialisable GetSerialisedOrderData(OrderID _orderID)
	{
		ServerOrderData result = null;
		for (int i = 0; i < m_activeOrders.Count; i++)
		{
			ServerOrderData serverOrderData = m_activeOrders[i];
			if (serverOrderData.ID == _orderID)
			{
				result = serverOrderData;
				break;
			}
		}
		return result;
	}
}
