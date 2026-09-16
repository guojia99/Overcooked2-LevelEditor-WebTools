public interface IServerRoundTimer
{
	bool IsSuppressed { get; }

	SuppressionController Suppressor { get; }

	float TimeElapsed { get; }

	void Initialise();

	bool TimeExpired();

	void Update();
}
