using System.Collections;
using System.Collections.Generic;

public interface ISaveManager
{
	MetaGameProgress GetMetaGameProgress();

	IEnumerator<SaveLoadResult?> LoadProfile(GamepadUser _user);

	IEnumerator<SaveLoadResult?> LoadSave(SaveMode _type, int _slot, int _dlcNumber);

	void UnloadProfile();

	void SaveMetaProgress(SaveSystemCallback _finished = null);

	IEnumerator SaveData(SaveMode _mode, int _slot, int _dlcNumber, SaveSystemCallback _finished = null);

	IEnumerator HasMetaSaveFile(ReturnValue<SaveLoadResult> _result);

	IEnumerator HasSaveFile(SaveMode _type, int _slot, int _dlcNumber, ReturnValue<SaveLoadResult> _result);

	void DeleteSave(SaveMode _type, int _slot, int _dlcNumber, CallbackVoid _callback = null);

	void DeleteMetaSave(CallbackVoid _callback = null);

	void RegisterOnIdle(GenericVoid _callback);

	void UnregisterOnIdle(GenericVoid _callback);

	void CreateMetaSession();

	void DestroyMetaSession();
}
