using BitStream;
using Team17.Online.Multiplayer.Messaging;

public class IngredientContainerMessage : Serialisable
{
	public enum MessageType
	{
		ContentsChanged = 0,
		ActiveState = 1
	}

	private const int m_kBitsPerType = 1;

	private MessageType m_type;

	private AssembledDefinitionNode[] m_contents;

	private bool m_activeState;

	public MessageType Type
	{
		get
		{
			return m_type;
		}
	}

	public AssembledDefinitionNode[] Contents
	{
		get
		{
			return m_contents;
		}
	}

	public bool ActiveState
	{
		get
		{
			return m_activeState;
		}
	}

	public void Initialise(AssembledDefinitionNode[] _contents)
	{
		m_contents = _contents;
		m_type = MessageType.ContentsChanged;
	}

	public void Initialise(bool _activeState)
	{
		m_activeState = _activeState;
		m_type = MessageType.ActiveState;
	}

	public void Serialise(BitStreamWriter writer)
	{
		writer.Write((uint)m_type, 1);
		switch (m_type)
		{
		case MessageType.ActiveState:
			writer.Write(m_activeState);
			break;
		case MessageType.ContentsChanged:
			if (m_contents != null)
			{
				CompositeAssembledNode compositeAssembledNode = new CompositeAssembledNode();
				compositeAssembledNode.m_composition = m_contents;
				writer.Write((uint)AssembledDefinitionNodeFactory.GetNodeType(compositeAssembledNode), 4);
				compositeAssembledNode.Serialise(writer);
			}
			break;
		}
	}

	public bool Deserialise(BitStreamReader reader)
	{
		m_type = (MessageType)reader.ReadByte(1);
		switch (m_type)
		{
		case MessageType.ActiveState:
			m_activeState = reader.ReadBit();
			break;
		case MessageType.ContentsChanged:
		{
			AssembledDefinitionNode assembledDefinitionNode = AssembledDefinitionNodeFactory.CreateNode((int)reader.ReadUInt32(4));
			CompositeAssembledNode compositeAssembledNode = assembledDefinitionNode as CompositeAssembledNode;
			if (compositeAssembledNode != null)
			{
				compositeAssembledNode.Deserialise(reader);
				m_contents = compositeAssembledNode.m_composition;
			}
			break;
		}
		}
		return true;
	}
}
