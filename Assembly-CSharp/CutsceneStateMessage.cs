using BitStream;
using Team17.Online.Multiplayer.Messaging;

public class CutsceneStateMessage : Serialisable
{
	public void Serialise(BitStreamWriter _writer)
	{
	}

	public bool Deserialise(BitStreamReader _reader)
	{
		return true;
	}
}
