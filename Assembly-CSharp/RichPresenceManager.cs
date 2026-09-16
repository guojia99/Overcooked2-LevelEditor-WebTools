public class RichPresenceManager : RichPresenceMangerBase
{
	public static void SetGameMode(GameMode mode)
	{
		if (RichPresenceMangerBase.m_gameMode != mode)
		{
			RichPresenceMangerBase.m_gameMode = mode;
			if (RichPresenceMangerBase.OnGameModeSet != null)
			{
				RichPresenceMangerBase.OnGameModeSet();
			}
		}
	}
}
