using BitStream;

namespace Team17.Online.Multiplayer.Messaging
{
	public class ResumeObjectSyncMessage<T> : Serialisable where T : Serialisable, new()
	{
		public uint EntityID;

		public T Data = new T();

		private EntityMessageHeader m_entityHeader = new EntityMessageHeader();

		public void Initialise(uint _entityID, T _data)
		{
			EntityID = _entityID;
			Data = _data;
		}

		public void Serialise(BitStreamWriter writer)
		{
			EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(EntityID);
			entry.m_Header.Serialise(writer);
			writer.Write(Data != null);
			if (Data != null)
			{
				Data.Serialise(writer);
			}
		}

		public bool Deserialise(BitStreamReader reader)
		{
			m_entityHeader.Deserialise(reader);
			EntityID = m_entityHeader.m_uEntityID;
			if (reader.ReadBit())
			{
				Data.Deserialise(reader);
			}
			return true;
		}
	}
}
