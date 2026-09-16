public abstract class AchievementManager : Manager
{
	protected IPlayerManager m_playerManager;

	protected StatSystem m_StatSystem;

	protected virtual void Awake()
	{
		m_playerManager = GameUtils.RequireManagerInterface<IPlayerManager>();
		m_StatSystem = base.gameObject.RequireComponent<StatSystem>();
	}

	public virtual void Init()
	{
	}

	public virtual void Unload()
	{
	}

	protected virtual void OnDestroy()
	{
	}

	protected virtual void SetProgress(int trophyId, float progress)
	{
	}

	protected virtual void Unlock(int trophyId)
	{
	}
}
