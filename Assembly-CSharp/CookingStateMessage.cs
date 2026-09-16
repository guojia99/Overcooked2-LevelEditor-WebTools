using BitStream;
using Team17.Online.Multiplayer.Messaging;

public class CookingStateMessage : Serialisable
{
	private const int kNumStateBits = 4;

	public CookingUIController.State m_cookingState;

	public float m_cookingProgress;

	public void Serialise(BitStreamWriter writer)
	{
		writer.Write((uint)m_cookingState, 4);
		writer.Write(m_cookingProgress);
	}

	public bool Deserialise(BitStreamReader reader)
	{
		m_cookingState = (CookingUIController.State)reader.ReadUInt32(4);
		m_cookingProgress = reader.ReadFloat32();
		return true;
	}
}
