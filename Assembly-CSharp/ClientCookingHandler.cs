using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientCookingHandler : ClientSynchroniserBase, IClientCookable, IClientCookingRegionNotified, IBaseCookable
{
	private CookingHandler m_cookingHandler;

	private IClientCookingNotifed[] m_ICookingNotified;

	private CookingUIController m_gui;

	private float m_hideAfterTime = float.MinValue;

	private float m_cachedCookingProgress = -1f;

	private CookingUIController.State m_cachedCookingState;

	private static StaticList<ClientCookingHandler> s_burntCookables = new StaticList<ClientCookingHandler>();

	private bool m_isInCookingRegion;

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

	public static event VoidGeneric<ClientCookingHandler> OnBurntCookableAdded
	{
		add
		{
			s_burntCookables.OnObjectAdded += value;
		}
		remove
		{
			s_burntCookables.OnObjectAdded -= value;
		}
	}

	public static event VoidGeneric<ClientCookingHandler> OnBurntCookableRemoved
	{
		add
		{
			s_burntCookables.OnObjectRemoved += value;
		}
		remove
		{
			s_burntCookables.OnObjectRemoved -= value;
		}
	}

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

	public override void StartSynchronising(Component synchronisedObject)
	{
		GameObject gameObject = GameUtils.InstantiateUIController(GetCookingHandler().m_cookingUIPrefab.gameObject, "HoverIconCanvas");
		m_gui = gameObject.GetComponent<CookingUIController>();
		m_gui.SetFollowTransform(NetworkUtils.FindVisualRoot(base.gameObject), Vector3.zero);
		m_gui.SetFollowOffset(GetCookingHandler().m_cookingUIPrefabOffset);
		m_ICookingNotified = base.gameObject.RequestInterfacesRecursive<IClientCookingNotifed>();
	}

	public override EntityType GetEntityType()
	{
		return EntityType.CookingState;
	}

	public override void ApplyServerUpdate(Serialisable serialisable)
	{
		CookingHandler cookingHandler = GetCookingHandler();
		CookingStateMessage cookingStateMessage = (CookingStateMessage)serialisable;
		bool flag = false;
		if (m_cachedCookingProgress != cookingStateMessage.m_cookingProgress)
		{
			m_cachedCookingProgress = cookingStateMessage.m_cookingProgress;
			flag = true;
		}
		if (m_cachedCookingState != cookingStateMessage.m_cookingState)
		{
			if (m_cachedCookingState == CookingUIController.State.Ruined)
			{
				s_burntCookables.Remove(this);
			}
			m_cachedCookingState = cookingStateMessage.m_cookingState;
			flag = true;
			this.m_cookingStateChangedCallback(m_cachedCookingState);
			if (m_cachedCookingState == CookingUIController.State.Ruined)
			{
				s_burntCookables.Add(this);
			}
		}
		if (!flag)
		{
			return;
		}
		m_ICookingNotified = base.gameObject.RequestInterfacesRecursive<IClientCookingNotifed>();
		float num = cookingStateMessage.m_cookingProgress / cookingHandler.m_cookingtime;
		for (int i = 0; i < m_ICookingNotified.Length; i++)
		{
			IClientCookingNotifed clientCookingNotifed = m_ICookingNotified[i];
			clientCookingNotifed.OnCookingPropChanged(num);
		}
		if (cookingStateMessage.m_cookingProgress > 0.001f)
		{
			if (m_gui != null)
			{
				m_gui.gameObject.SetActive(base.gameObject.activeInHierarchy);
				m_gui.SetProgress(num);
				float b = Mathf.Clamp(2f * cookingHandler.m_cookingtime - 0.5f, cookingHandler.m_cookingtime, 2f * cookingHandler.m_cookingtime);
				m_gui.SetOverDoingAmount(MathUtils.ClampedRemap(cookingStateMessage.m_cookingProgress, cookingHandler.m_cookingtime, b, 0f, 1f));
			}
			if (m_isInCookingRegion)
			{
				m_hideAfterTime = 1f;
				if (m_gui != null)
				{
					m_gui.SetState(cookingStateMessage.m_cookingState);
				}
				for (int j = 0; j < m_ICookingNotified.Length; j++)
				{
					IClientCookingNotifed clientCookingNotifed2 = m_ICookingNotified[j];
					clientCookingNotifed2.OnCookingStarted();
				}
				return;
			}
			m_hideAfterTime = 0f;
			if (m_gui != null)
			{
				if (m_cachedCookingState != CookingUIController.State.Progressing)
				{
					m_gui.SetState(CookingUIController.State.Idle);
				}
				else
				{
					m_gui.SetState(cookingStateMessage.m_cookingState);
				}
			}
		}
		else
		{
			if (m_gui != null)
			{
				m_gui.gameObject.SetActive(false);
			}
			m_hideAfterTime = float.MinValue;
			for (int k = 0; k < m_ICookingNotified.Length; k++)
			{
				IClientCookingNotifed clientCookingNotifed3 = m_ICookingNotified[k];
				clientCookingNotifed3.OnCookingFinished();
			}
		}
	}

	public CookedCompositeOrderNode.CookingProgress GetCookedOrderState()
	{
		return GetCookingHandler().GetCookedOrderState(m_cachedCookingProgress);
	}

	public float GetCookingProgress()
	{
		return m_cachedCookingProgress;
	}

	public GameLoopingAudioTag GetSizzleSoundTag()
	{
		return GetCookingHandler().m_cookingType.m_sizzleSound;
	}

	private CookingHandler GetCookingHandler()
	{
		if (m_cookingHandler == null)
		{
			m_cookingHandler = base.gameObject.RequireComponent<CookingHandler>();
		}
		return m_cookingHandler;
	}

	public override void UpdateSynchronising()
	{
		float hideAfterTime = m_hideAfterTime;
		m_hideAfterTime -= TimeManager.GetDeltaTime(base.gameObject);
		if (hideAfterTime >= 0f && m_hideAfterTime < 0f)
		{
			if (m_cachedCookingState == CookingUIController.State.OverDoing && m_gui != null)
			{
				m_gui.SetState(CookingUIController.State.Idle);
			}
			for (int i = 0; i < m_ICookingNotified.Length; i++)
			{
				IClientCookingNotifed clientCookingNotifed = m_ICookingNotified[i];
				clientCookingNotifed.OnCookingFinished();
			}
		}
	}

	public bool IsBurning()
	{
		return GetCookingProgress() > 2f * AccessCookingTime;
	}

	public CookingStationType GetRequiredStationType()
	{
		return GetCookingHandler().m_stationType;
	}

	protected override void OnDisable()
	{
		base.OnDisable();
		if (m_gui != null)
		{
			m_gui.SetState(CookingUIController.State.Idle);
			m_gui.gameObject.SetActive(false);
		}
	}

	public void EnterCookingRegion()
	{
		m_isInCookingRegion = true;
	}

	public void ExitCookingRegion()
	{
		m_isInCookingRegion = false;
	}
}
