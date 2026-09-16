public interface IClientConveyenceReceiver
{
	void InformStartingConveyToMe();

	void InformEndingConveyToMe();

	bool IsReceiving();

	void RegisterRefreshedConveyToCallback(CallbackVoid _callback);

	void UnregisterRefreshedConveyToCallback(CallbackVoid _callback);
}
