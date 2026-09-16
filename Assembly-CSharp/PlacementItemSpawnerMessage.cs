using BitStream;
using Team17.Online.Multiplayer.Messaging;

public class PlacementItemSpawnerMessage : Serialisable
{
	public void Serialise(BitStreamWriter writer)
	{
	}

	public bool Deserialise(BitStreamReader reader)
	{
		return true;
	}
}
