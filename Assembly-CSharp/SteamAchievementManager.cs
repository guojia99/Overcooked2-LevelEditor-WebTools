using Steamworks;

public class SteamAchievementManager : AchievementManager
{
	private AppId_t m_appId;

	protected Callback<UserStatsReceived_t> m_userStatsRecievedResult;

	protected Callback<UserStatsStored_t> m_userStatsStoredResult;

	protected Callback<UserAchievementStored_t> m_achievementResult;

	public override void Init()
	{
		m_appId = SteamUtils.GetAppID();
		m_userStatsRecievedResult = Callback<UserStatsReceived_t>.Create(OnUserStatsReceived);
		m_userStatsStoredResult = Callback<UserStatsStored_t>.Create(OnUserStatsStored);
		m_achievementResult = Callback<UserAchievementStored_t>.Create(OnAchievementStored);
		SteamUserStats.RequestCurrentStats();
	}

	private void OnUserStatsReceived(UserStatsReceived_t param)
	{
	}

	private void OnUserStatsStored(UserStatsStored_t param)
	{
	}

	private void OnAchievementStored(UserAchievementStored_t param)
	{
	}

	protected override void Unlock(int trophyId)
	{
		string trophyApiName = m_StatSystem.GetTrophyApiName(trophyId);
		SteamUserStats.SetAchievement(trophyApiName);
		SteamUserStats.StoreStats();
	}
}
