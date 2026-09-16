namespace GameModes
{
	public abstract class ServerGameModeBase : IServerMode
	{
		public ServerGameModeBase(Config config)
		{
		}

		public virtual void Setup(ServerContext context, SessionConfig config, ref ServerSetupData setupData)
		{
		}

		public virtual void Begin()
		{
		}

		public virtual void Update()
		{
		}

		public virtual void End()
		{
		}
	}
}
