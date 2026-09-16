public interface IClientRoundTimer
{
	bool IsSuppressed { get; }

	SuppressionController Suppressor { get; }

	float TimeElapsed { get; }

	void Initialise();

	void Update();

	void Zero();
}
