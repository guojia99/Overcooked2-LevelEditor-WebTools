namespace Team17.Online
{
	public static class OnlineMultiplayerConfig
	{
		public static uint MaxPlayers
		{
			get
			{
				return 4u;
			}
		}

		public static uint MaxTransportMessageSize
		{
			get
			{
				return 1024u;
			}
		}

		public static uint CodeVersion
		{
			get
			{
				return 17u;
			}
		}

		public static uint MaxSocketIterationsPerUpdate
		{
			get
			{
				return 100u;
			}
		}

		public static ushort MaxBrowsedSessionsToEnumerate
		{
			get
			{
				return 10;
			}
		}
	}
}
