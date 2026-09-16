namespace GameModes
{
	public abstract class ClientGameModeBase : IClientMode
	{
		public ClientGameModeBase(Config config)
		{
		}

		public virtual void Setup(ClientContext context, SessionConfig config, ref ClientSetupData setupData)
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
