using UnityEngine;

public class ClientModifiableRoundTimer : ClientRoundTimer
{
	protected const int k_timeMax = 5999;

	private static readonly DataStore.Id k_timeAddedId = new DataStore.Id("time.added");

	public override void Initialise()
	{
		base.Initialise();
		m_roundTimer = m_timeLimit;
		m_dataStore.Write(ClientRoundTimer.k_timeUpdatedId, m_roundTimer);
	}

	public override void Zero()
	{
		m_roundTimer = 0f;
		Update();
	}

	public void AddTime(int time)
	{
		m_roundTimer += time;
		m_roundTimer = Mathf.Clamp(m_roundTimer, 0f, 5999f);
		m_dataStore.Write(k_timeAddedId, time);
	}

	public override void Update()
	{
		m_suppressionController.UpdateSuppressors();
		if (!m_suppressionController.IsSuppressed() && !DebugManager.Instance.GetOption("Freeze time"))
		{
			m_roundTimer -= TimeManager.GetDeltaTime(LayerMask.NameToLayer("Default"));
			m_dataStore.Write(ClientRoundTimer.k_timeUpdatedId, m_roundTimer);
		}
	}
}
