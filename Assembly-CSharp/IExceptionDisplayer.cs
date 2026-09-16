internal interface IExceptionDisplayer
{
	void Initialize();

	void OnGUI();

	void Display(string exceptionString, string stackTrace, bool bJustOccured);
}
