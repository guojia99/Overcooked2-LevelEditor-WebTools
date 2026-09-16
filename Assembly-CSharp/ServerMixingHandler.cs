using Team17.Online.Multiplayer.Messaging;

public class ServerMixingHandler : ServerSynchroniserBase, IMixable
{
	private MixingHandler m_mixingHandler;

	private MixingStateMessage m_serverData = new MixingStateMessage();

	public StateChanged m_stateChangedCallback = delegate
	{
	};

	public float AccessMixingTime
	{
		get
		{
			return GetMixingHandler().m_mixingTime;
		}
	}

	public override EntityType GetEntityType()
	{
		return EntityType.MixingState;
	}

	public override Serialisable GetServerUpdate()
	{
		return m_serverData;
	}

	protected override void OnEnable()
	{
		base.OnEnable();
		SetMixingProgress(m_serverData.m_mixingProgress);
	}

	protected override void OnDisable()
	{
		base.OnDisable();
	}

	public MixedCompositeOrderNode.MixingProgress GetMixedOrderState()
	{
		return GetMixingHandler().GetMixedOrderState(m_serverData.m_mixingProgress);
	}

	public void SetMixingProgress(float _mixingProgress)
	{
		MixingHandler mixingHandler = GetMixingHandler();
		m_serverData.m_mixingProgress = _mixingProgress;
		CookingUIController.State mixingState = m_serverData.m_mixingState;
		if (IsOverMixed())
		{
			m_serverData.m_mixingState = CookingUIController.State.Ruined;
		}
		else if (IsMixed())
		{
			if (m_serverData.m_mixingState == CookingUIController.State.Progressing)
			{
				m_serverData.m_mixingState = CookingUIController.State.Completed;
			}
			if (_mixingProgress > 1.3f * mixingHandler.m_mixingTime)
			{
				m_serverData.m_mixingState = CookingUIController.State.OverDoing;
			}
		}
		else
		{
			m_serverData.m_mixingState = CookingUIController.State.Progressing;
		}
		if (m_serverData.m_mixingState != mixingState)
		{
			m_stateChangedCallback(m_serverData.m_mixingState);
		}
	}

	public MixingHandler GetMixingHandler()
	{
		if (m_mixingHandler == null)
		{
			m_mixingHandler = base.gameObject.RequireComponent<MixingHandler>();
		}
		return m_mixingHandler;
	}

	public float GetMixingProgress()
	{
		return m_serverData.m_mixingProgress;
	}

	public bool IsMixed()
	{
		return m_serverData.m_mixingProgress >= AccessMixingTime;
	}

	public bool IsOverMixed()
	{
		return m_serverData.m_mixingProgress > 2f * AccessMixingTime;
	}

	public bool Mix(float _deltaTime)
	{
		if (!IsOverMixed())
		{
			SetMixingProgress(m_serverData.m_mixingProgress + _deltaTime);
			return true;
		}
		return false;
	}
}
