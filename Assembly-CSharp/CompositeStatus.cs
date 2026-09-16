public class CompositeStatus : IConnectionModeSwitchStatus
{
	public IConnectionModeSwitchStatus m_TaskSubStatus;

	public bool bFinalTask;

	public eConnectionModeSwitchProgress GetProgress()
	{
		if (m_TaskSubStatus == null)
		{
			return eConnectionModeSwitchProgress.NotStarted;
		}
		if (bFinalTask && m_TaskSubStatus.GetProgress() != eConnectionModeSwitchProgress.NotStarted)
		{
			return m_TaskSubStatus.GetProgress();
		}
		return eConnectionModeSwitchProgress.InProgress;
	}

	public string GetLocalisedProgressDescription()
	{
		if (m_TaskSubStatus != null)
		{
			return m_TaskSubStatus.GetLocalisedProgressDescription();
		}
		return Localization.Get("Online.ConnectionMode.Progress.NotStarted");
	}

	public eConnectionModeSwitchResult GetResult()
	{
		if (m_TaskSubStatus == null)
		{
			return eConnectionModeSwitchResult.NotAvailableYet;
		}
		if (bFinalTask)
		{
			return m_TaskSubStatus.GetResult();
		}
		return eConnectionModeSwitchResult.NotAvailableYet;
	}

	public string GetLocalisedResultDescription()
	{
		if (m_TaskSubStatus != null)
		{
			return m_TaskSubStatus.GetLocalisedResultDescription();
		}
		return string.Empty;
	}

	public virtual bool DisplayPlatformDialog()
	{
		if (m_TaskSubStatus != null)
		{
			return m_TaskSubStatus.DisplayPlatformDialog();
		}
		return false;
	}

	public virtual IConnectionModeSwitchStatus Clone()
	{
		CompositeStatus compositeStatus = new CompositeStatus();
		compositeStatus.m_TaskSubStatus = m_TaskSubStatus.Clone();
		compositeStatus.bFinalTask = bFinalTask;
		return compositeStatus;
	}
}
