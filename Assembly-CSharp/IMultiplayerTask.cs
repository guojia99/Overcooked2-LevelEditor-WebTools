public interface IMultiplayerTask
{
	void Start(object startData);

	void Stop();

	void Update();

	IConnectionModeSwitchStatus GetStatus();

	object GetData();
}
