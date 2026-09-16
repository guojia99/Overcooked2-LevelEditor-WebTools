public interface IClientMixingNotifed
{
	void OnMixingStarted();

	void OnMixingFinished();

	void OnMixingPropChanged(float newProp);
}
