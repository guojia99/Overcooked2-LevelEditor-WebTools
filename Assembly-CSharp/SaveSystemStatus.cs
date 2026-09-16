public struct SaveSystemStatus
{
	public enum SaveStatus
	{
		InProgress = 0,
		Retry = 1,
		Complete = 2,
		COUNT = 3
	}

	public SaveStatus Status;

	public SaveLoadResult Result;

	public SaveSystemStatus(SaveStatus _status, SaveLoadResult _result)
	{
		Status = _status;
		Result = _result;
	}
}
