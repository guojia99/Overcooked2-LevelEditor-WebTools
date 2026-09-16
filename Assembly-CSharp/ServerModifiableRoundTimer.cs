using UnityEngine;

public class ServerModifiableRoundTimer : ServerRoundTimer
{
	protected const int k_timeMax = 5999;

	public override float TimeElapsed
	{
		get
		{
			return m_roundTimer;
		}
	}

	public override void Initialise()
	{
		base.Initialise();
		m_roundTimer = m_timeLimit;
	}

	public override bool TimeExpired()
	{
		return m_roundTimer <= 0f;
	}

	public void AddTime(int time)
	{
		m_roundTimer += time;
		m_roundTimer = Mathf.Clamp(m_roundTimer, 0f, 5999f);
	}

	public override void Update()
	{
		m_suppressionController.UpdateSuppressors();
		if (!m_suppressionController.IsSuppressed() && !DebugManager.Instance.GetOption("Freeze time"))
		{
			m_roundTimer -= TimeManager.GetDeltaTime(LayerMask.NameToLayer("Default"));
		}
	}
}
