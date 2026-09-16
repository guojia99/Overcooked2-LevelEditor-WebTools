namespace Team17.Online.Multiplayer.Messaging
{
	public interface ClientSynchroniser : Synchroniser
	{
		bool IsValidServerUpdateSequenceNumber(uint uSequence);

		void SetLastServerUpdateSequenceNumber(uint uSequence);

		bool IsValidLastUpdateTimeStamp(float timeStamp, float diff);

		void SetLastUpdateTimeStamp(float timeStamp);

		void ApplyServerUpdate(Serialisable serialisable);

		void ApplyServerEvent(Serialisable serialisable);
	}
}
