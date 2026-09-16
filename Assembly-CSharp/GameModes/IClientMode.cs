namespace GameModes
{
	public interface IClientMode
	{
		void Setup(ClientContext context, SessionConfig config, ref ClientSetupData setupData);

		void Begin();

		void Update();

		void End();
	}
}
