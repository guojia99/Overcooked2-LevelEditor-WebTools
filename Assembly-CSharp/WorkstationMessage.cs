using BitStream;
using Team17.Online.Multiplayer.Messaging;

public class WorkstationMessage : Serialisable
{
	public EntitySerialisationEntry m_interactor;

	public EntitySerialisationEntry m_item;

	public bool m_interacting;

	public EntityMessageHeader m_interactorHeader = new EntityMessageHeader();

	public EntityMessageHeader m_itemHeader = new EntityMessageHeader();

	public void Serialise(BitStreamWriter writer)
	{
		writer.Write(m_interacting);
		m_interactor.m_Header.Serialise(writer);
		if (m_interacting)
		{
			m_item.m_Header.Serialise(writer);
		}
	}

	public bool Deserialise(BitStreamReader reader)
	{
		m_interacting = reader.ReadBit();
		m_interactorHeader.Deserialise(reader);
		if (m_interacting)
		{
			m_itemHeader.Deserialise(reader);
		}
		return true;
	}
}
