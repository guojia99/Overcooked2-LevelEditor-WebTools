using UnityEngine;

public class ClientUnlimitedRoundTimer : IClientRoundTimer
{
	protected const int k_timeMax = 5999;

	private DataStore m_dataStore;

	private static readonly DataStore.Id k_timeUpdatedId = new DataStore.Id("time.updated");

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
		m_dataStore = GameUtils.RequireManager<DataStore>();
	}

	public void Zero()
	{
		m_roundTimer = 0f;
		Update();
	}

	public virtual void Update()
	{
		m_suppressionController.UpdateSuppressors();
		if (!m_suppressionController.IsSuppressed() && !DebugManager.Instance.GetOption("Freeze time"))
		{
			m_roundTimer += TimeManager.GetDeltaTime(LayerMask.NameToLayer("Default"));
			m_roundTimer = Mathf.Clamp(m_roundTimer, 0f, 5999f);
			m_dataStore.Write(k_timeUpdatedId, m_roundTimer);
		}
	}
}
