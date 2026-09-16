using UnityEngine;

public class ServerUnlimitedRoundTimer : IServerRoundTimer
{
	protected const int k_timeMax = 5999;

	protected float m_roundTimer;

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

	public float TimeElapsed
	{
		get
		{
			return m_roundTimer;
		}
	}

	public virtual void Initialise()
	{
	}

	public bool TimeExpired()
	{
		return false;
	}

	public virtual void Update()
	{
		m_suppressionController.UpdateSuppressors();
		if (!m_suppressionController.IsSuppressed() && !DebugManager.Instance.GetOption("Freeze time"))
		{
			m_roundTimer += TimeManager.GetDeltaTime(LayerMask.NameToLayer("Default"));
			m_roundTimer = Mathf.Clamp(m_roundTimer, 0f, 5999f);
		}
	}
}
