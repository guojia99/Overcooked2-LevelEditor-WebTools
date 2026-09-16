public class MultiplayerOperation
{
	private bool m_bDone;

	private IMultiplayerTask[] m_Tasks;

	private int m_CurrentTaskIndex;

	private CompositeStatus m_Status = new CompositeStatus();

	public void Start(IMultiplayerTask[] tasks)
	{
		m_bDone = false;
		m_Tasks = tasks;
		m_CurrentTaskIndex = 0;
		m_Status.bFinalTask = false;
		m_Status.m_TaskSubStatus = m_Tasks[m_CurrentTaskIndex].GetStatus();
		m_Tasks[m_CurrentTaskIndex].Start(null);
	}

	public void Stop()
	{
		if (m_Tasks != null)
		{
			for (int i = 0; i < m_Tasks.Length; i++)
			{
				m_Tasks[i].Stop();
			}
		}
		m_bDone = false;
		m_Tasks = null;
		m_CurrentTaskIndex = 0;
		m_Status.bFinalTask = false;
		m_Status.m_TaskSubStatus = null;
	}

	public object GetTaskData()
	{
		return m_Tasks[m_CurrentTaskIndex].GetData();
	}

	public IConnectionModeSwitchStatus GetStatus()
	{
		return m_Status;
	}

	public void Update()
	{
		if (m_bDone)
		{
			return;
		}
		IMultiplayerTask multiplayerTask = m_Tasks[m_CurrentTaskIndex];
		if (multiplayerTask.GetStatus().GetProgress() == eConnectionModeSwitchProgress.Complete)
		{
			if (multiplayerTask.GetStatus().GetResult() == eConnectionModeSwitchResult.Success)
			{
				if (m_CurrentTaskIndex < m_Tasks.Length - 1)
				{
					m_CurrentTaskIndex++;
					m_Status.m_TaskSubStatus = m_Tasks[m_CurrentTaskIndex].GetStatus();
					m_Status.bFinalTask = m_CurrentTaskIndex == m_Tasks.Length - 1;
					m_Tasks[m_CurrentTaskIndex].Start(multiplayerTask.GetData());
				}
				else
				{
					m_bDone = true;
					m_Status.bFinalTask = true;
				}
			}
			else
			{
				m_bDone = true;
				m_Status.bFinalTask = true;
			}
		}
		else
		{
			multiplayerTask.Update();
		}
	}
}
