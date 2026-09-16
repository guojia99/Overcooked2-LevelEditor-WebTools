namespace GameModes
{
	public interface IServerMode
	{
		void Setup(ServerContext context, SessionConfig config, ref ServerSetupData setupData);

		void Begin();

		void Update();

		void End();
	}
}
