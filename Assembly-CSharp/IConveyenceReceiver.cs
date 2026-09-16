using System.Collections;

public interface IConveyenceReceiver
{
	void InformStartingConveyToMe();

	IEnumerator ConveyToMe(ServerConveyorStation _priorConveyor, IAttachment _object);

	void InformEndingConveyToMe();

	bool IsReceiving();

	bool CanConveyTo(IAttachment _itemToConvey);

	void RegisterRefreshedConveyToCallback(CallbackVoid _callback);

	void UnregisterRefreshedConveyToCallback(CallbackVoid _callback);

	void RefreshConveyTo();

	void RegisterAllowConveyToCallback(Generic<bool> _allowConveyCallback);

	void UnregisterAllowConveyToCallback(Generic<bool> _allowConveyCallback);
}
