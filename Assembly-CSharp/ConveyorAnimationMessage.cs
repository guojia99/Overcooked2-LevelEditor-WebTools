using BitStream;
using Team17.Online.Multiplayer.Messaging;

public class ConveyorAnimationMessage : Serialisable
{
	public const int kBitsPerState = 2;

	public TriggerAnimationOnConveyor.State m_state;

	public void Initialise(TriggerAnimationOnConveyor.State _state)
	{
		m_state = _state;
	}

	public void Serialise(BitStreamWriter writer)
	{
		writer.Write((uint)m_state, 2);
	}

	public bool Deserialise(BitStreamReader reader)
	{
		m_state = (TriggerAnimationOnConveyor.State)reader.ReadUInt32(2);
		return true;
	}
}
