using Team17.Online.Multiplayer.Connection;
using Team17.Online.Multiplayer.Messaging;

namespace Team17.Online.Multiplayer
{
	public interface NetworkPeer
	{
		event GenericVoid<IOnlineMultiplayerSessionUserId, MessageType, Serialisable, uint, bool> OnMessageReceived;

		event GenericVoid OnLeftSession;

		void HandleReceivedBytesFromConnection(NetworkConnection connection, byte[] data, int size);

		void HandleConnectionLost(IOnlineMultiplayerSessionUserId sessionUserId, NetworkConnection connection);

		void HandleLocalLoopbackConnectionLost(NetworkConnection connection);

		void HandleDisconnectMessage(NetworkConnection connection);

		void Dispatch();

		ConnectionStats GetConnectionStats(bool bReliable);

		void SetLatencyTestPaused(bool paused);

		NetworkMessageTracker GetTracker();
	}
}
