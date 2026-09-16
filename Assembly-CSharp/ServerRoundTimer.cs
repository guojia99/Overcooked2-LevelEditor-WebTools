using UnityEngine;

public class ServerRoundTimer : IServerRoundTimer
{
	protected int m_timeLeft;

	protected float m_roundTimer;

	protected float m_timeLimit;

	protected SuppressionController m_suppressionController = new SuppressionController();

	public SuppressionController Suppressor
	{
		get
		{
			return m_suppressionController;
		}
	}

	public bool IsSuppressed
	{
		get
		{
			return m_suppressionController.IsSuppressed();
		}
	}

	public virtual float TimeElapsed
	{
		get
		{
			return m_roundTimer;
		}
	}

	public virtual void Initialise()
	{
		KitchenLevelConfigBase kitchenLevelConfigBase = GameUtils.GetLevelConfig() as KitchenLevelConfigBase;
		m_timeLimit = kitchenLevelConfigBase.GetTimeLimit();
	}

	public virtual bool TimeExpired()
	{
		return m_timeLeft <= 0;
	}

	public virtual void Update()
	{
		m_suppressionController.UpdateSuppressors();
		if (!m_suppressionController.IsSuppressed() && !DebugManager.Instance.GetOption("Freeze time"))
		{
			m_roundTimer += TimeManager.GetDeltaTime(LayerMask.NameToLayer("Default"));
		}
		int num = Mathf.CeilToInt(Mathf.Max(m_timeLimit - m_roundTimer, 0f));
		if (num != m_timeLeft)
		{
			m_timeLeft = num;
		}
	}
}
