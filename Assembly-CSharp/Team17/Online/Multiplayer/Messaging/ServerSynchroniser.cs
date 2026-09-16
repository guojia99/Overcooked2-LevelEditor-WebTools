namespace Team17.Online.Multiplayer.Messaging
{
	public interface ServerSynchroniser : Synchroniser
	{
		void Initialise(uint uEntityId, uint uComponentId);

		Serialisable GetServerUpdate();

		bool HasTargetedServerUpdates();

		Serialisable GetServerUpdateForRecipient(IOnlineMultiplayerSessionUserId recipient);
	}
}
