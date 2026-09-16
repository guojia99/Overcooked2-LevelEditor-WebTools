using BitStream;
using Team17.Online.Multiplayer.Messaging;

public class WorldMapInfoPopupMessage : Serialisable
{
	public bool Deserialise(BitStreamReader reader)
	{
		return true;
	}

	public void Serialise(BitStreamWriter writer)
	{
	}
}
