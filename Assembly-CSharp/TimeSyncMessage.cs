using BitStream;
using Team17.Online.Multiplayer.Messaging;

public class TimeSyncMessage : Serialisable
{
	public float fTime;

	public void Initialise(float _fTime)
	{
		fTime = _fTime;
	}

	public void Serialise(BitStreamWriter writer)
	{
		writer.Write(fTime);
	}

	public bool Deserialise(BitStreamReader reader)
	{
		fTime = reader.ReadFloat32();
		return true;
	}
}
