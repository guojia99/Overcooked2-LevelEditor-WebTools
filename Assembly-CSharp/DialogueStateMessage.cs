using BitStream;
using Team17.Online.Multiplayer.Messaging;

public class DialogueStateMessage : Serialisable
{
	private const int c_bitsPerDialogueID = 24;

	private const int c_bitsPerState = 8;

	public int m_dialogueID;

	public int m_state;

	public void Initialise(DialogueController.Dialogue _dialogue, int _newState)
	{
		m_dialogueID = _dialogue.UniqueID;
		m_state = _newState;
	}

	public void Serialise(BitStreamWriter _writer)
	{
		_writer.Write((uint)m_dialogueID, 24);
		_writer.Write((uint)m_state, 8);
	}

	public bool Deserialise(BitStreamReader _reader)
	{
		m_dialogueID = (int)_reader.ReadUInt32(24);
		m_state = (int)_reader.ReadUInt32(8);
		return true;
	}
}
