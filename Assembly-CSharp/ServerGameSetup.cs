public static class ServerGameSetup
{
	private static GameMode m_Mode = GameMode.COUNT;

	public static GameMode Mode
	{
		get
		{
			return m_Mode;
		}
		set
		{
			if (m_Mode != value)
			{
				m_Mode = value;
				if (ServerMessenger.GameSetup(m_Mode))
				{
				}
			}
		}
	}

	public static void BecomeClient()
	{
		m_Mode = GameMode.COUNT;
	}
}
