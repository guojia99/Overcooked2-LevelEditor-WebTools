public abstract class KitchenTask : IKitchenTask
{
	protected KitchenTaskStatus m_status;

	protected IPlayerManager m_IPlayerManager;

	public bool isRunning
	{
		get
		{
			return m_status == KitchenTaskStatus.Running;
		}
	}

	public event GenericVoid<KitchenTaskResult> onComplete;

	public KitchenTask()
	{
		m_status = KitchenTaskStatus.NotStarted;
		m_IPlayerManager = GameUtils.RequireManagerInterface<IPlayerManager>();
	}

	public virtual void Start()
	{
	}

	public virtual void CleanUp()
	{
		this.onComplete = null;
	}

	public KitchenTaskStatus GetStatus()
	{
		return m_status;
	}

	public virtual void Update()
	{
	}

	protected virtual void TaskComplete(KitchenTaskResult result)
	{
		m_status = KitchenTaskStatus.Complete;
		if (this.onComplete != null)
		{
			this.onComplete(result);
		}
	}
}
