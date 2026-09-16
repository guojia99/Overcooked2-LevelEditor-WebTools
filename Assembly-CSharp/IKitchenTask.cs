public interface IKitchenTask
{
	bool isRunning { get; }

	void Start();

	void Update();

	KitchenTaskStatus GetStatus();
}
