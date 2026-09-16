namespace Team17.Online.Multiplayer.Messaging
{
	public enum TechMessageType
	{
		ReliableMessageBatch = 0,
		UnreliableMessageBatch = 1,
		ReliableGameMessage = 2,
		UnreliableGameMessage = 3,
		ReliableMultiPart = 4,
		Disconnect = 5
	}
}
