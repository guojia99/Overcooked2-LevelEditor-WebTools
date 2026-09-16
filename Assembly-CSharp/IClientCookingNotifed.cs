public interface IClientCookingNotifed
{
	void OnCookingStarted();

	void OnCookingFinished();

	void OnCookingPropChanged(float newProp);
}
