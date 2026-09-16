using BitStream;
using GameModes;
using Team17.Online.Multiplayer.Messaging;

public class SetupCoopSessionNetworkMessage : Serialisable
{
	public int m_DLCID;

	public GameProgressDataNetworkMessage m_Progress = new GameProgressDataNetworkMessage();

	public SessionConfigSyncMessage m_sessionConfig = new SessionConfigSyncMessage();

	public void Serialise(BitStreamWriter writer)
	{
		bool flag = m_DLCID != -1;
		writer.Write(flag);
		if (flag)
		{
			writer.Write((uint)m_DLCID, 4);
		}
		m_Progress.Serialise(writer);
		m_sessionConfig.Serialise(writer);
	}

	public bool Deserialise(BitStreamReader reader)
	{
		if (reader.ReadBit())
		{
			m_DLCID = (int)reader.ReadUInt32(4);
		}
		else
		{
			m_DLCID = -1;
		}
		bool flag = true;
		flag &= m_Progress.Deserialise(reader);
		return flag & m_sessionConfig.Deserialise(reader);
	}
}
