using BitStream;
using Team17.Online.Multiplayer.Messaging;

public class ChefCarryMessage : Serialisable
{
	private static readonly int playerAttachTargetBits = GameUtils.GetRequiredBitCount(2);

	public uint m_carriableItem;

	public PlayerAttachTarget m_playerAttachTarget;

	public void Serialise(BitStreamWriter writer)
	{
		writer.Write(m_carriableItem, 10);
		writer.Write((uint)m_playerAttachTarget, playerAttachTargetBits);
	}

	public bool Deserialise(BitStreamReader reader)
	{
		m_carriableItem = reader.ReadUInt32(10);
		m_playerAttachTarget = (PlayerAttachTarget)reader.ReadUInt32(playerAttachTargetBits);
		return true;
	}
}
