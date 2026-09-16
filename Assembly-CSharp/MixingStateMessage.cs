using BitStream;
using Team17.Online.Multiplayer.Messaging;

public class MixingStateMessage : Serialisable
{
	private const int kNumStateBits = 4;

	public CookingUIController.State m_mixingState;

	public float m_mixingProgress;

	public void Serialise(BitStreamWriter writer)
	{
		writer.Write((uint)m_mixingState, 4);
		writer.Write(m_mixingProgress);
	}

	public bool Deserialise(BitStreamReader reader)
	{
		m_mixingState = (CookingUIController.State)reader.ReadUInt32(4);
		m_mixingProgress = reader.ReadFloat32();
		return true;
	}
}
