using System.Collections.Generic;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerCookingHandler : ServerSynchroniserBase, ICookable, IBaseCookable
{
	private static List<ServerCookingHandler> s_cookingHandlers = new List<ServerCookingHandler>();

	private CookingHandler m_cookingHandler;

	private CookingStateMessage m_ServerData = new CookingStateMessage();

	public float AccessCookingTime
	{
		get
		{
			return GetCookingHandler().m_cookingtime;
		}
	}

	public CookingStepData AccessCookingType
	{
		get
		{
			return GetCookingHandler().m_cookingType;
		}
	}

	private event CookingStateChanged m_cookingStateChangedCallback = delegate
	{
	};

	public event CookingStateChanged CookingStateChangedCallback
	{
		add
		{
			m_cookingStateChangedCallback += value;
		}
		remove
		{
			m_cookingStateChangedCallback -= value;
		}
	}

	public static IEnumerable<ServerCookingHandler> GetCookingHandlers()
	{
		return s_cookingHandlers;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
	}

	public override EntityType GetEntityType()
	{
		return EntityType.CookingState;
	}

	public override Serialisable GetServerUpdate()
	{
		return m_ServerData;
	}

	public CookedCompositeOrderNode.CookingProgress GetCookedOrderState()
	{
		return GetCookingHandler().GetCookedOrderState(m_ServerData.m_cookingProgress);
	}

	public CookingStationType GetRequiredStationType()
	{
		return GetCookingHandler().m_stationType;
	}

	public bool IsCooked()
	{
		return m_ServerData.m_cookingProgress >= AccessCookingTime;
	}

	public bool IsBurning()
	{
		return m_ServerData.m_cookingProgress > 2f * AccessCookingTime;
	}

	public float GetCookingProgress()
	{
		return m_ServerData.m_cookingProgress;
	}

	public void SetCookingProgress(float _cookingProgress)
	{
		CookingHandler cookingHandler = GetCookingHandler();
		m_ServerData.m_cookingProgress = _cookingProgress;
		CookingUIController.State cookingState = m_ServerData.m_cookingState;
		if (IsBurning())
		{
			m_ServerData.m_cookingState = CookingUIController.State.Ruined;
		}
		else if (_cookingProgress >= cookingHandler.m_cookingtime)
		{
			if (m_ServerData.m_cookingState == CookingUIController.State.Progressing)
			{
				m_ServerData.m_cookingState = CookingUIController.State.Completed;
			}
			if (_cookingProgress > 1.3f * cookingHandler.m_cookingtime)
			{
				m_ServerData.m_cookingState = CookingUIController.State.OverDoing;
			}
		}
		else
		{
			m_ServerData.m_cookingState = CookingUIController.State.Progressing;
		}
		if (m_ServerData.m_cookingState != cookingState)
		{
			this.m_cookingStateChangedCallback(m_ServerData.m_cookingState);
		}
	}

	public bool Cook(float _cookingDeltatTime)
	{
		if (!IsBurning())
		{
			SetCookingProgress(m_ServerData.m_cookingProgress + _cookingDeltatTime);
			return true;
		}
		return false;
	}

	public CookingHandler GetCookingHandler()
	{
		if (m_cookingHandler == null)
		{
			m_cookingHandler = base.gameObject.RequireComponent<CookingHandler>();
		}
		return m_cookingHandler;
	}

	protected override void OnEnable()
	{
		base.OnEnable();
		s_cookingHandlers.Add(this);
		SetCookingProgress(m_ServerData.m_cookingProgress);
	}

	protected override void OnDisable()
	{
		base.OnDisable();
		s_cookingHandlers.Remove(this);
	}
}
