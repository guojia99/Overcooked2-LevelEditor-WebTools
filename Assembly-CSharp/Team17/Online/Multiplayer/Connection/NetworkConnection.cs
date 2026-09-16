namespace Team17.Online.Multiplayer.Connection
{
	public interface NetworkConnection
	{
		IOnlineMultiplayerSessionUserId GetRemoteSessionUserId();

		bool SendMessage(byte[] data, int size, bool bReliable);

		void HandleReceivedBytes(byte[] data, int size);

		void Dispatch();

		void Disconnect();

		ConnectionStats GetConnectionStats(bool bReliable);

		bool CheckReceivedSequenceNumber(bool bReliable, uint sequenceNumber);

		void SetLatencyTestPaused(bool paused);

		bool GetLatencyTestPaused();
	}
}
