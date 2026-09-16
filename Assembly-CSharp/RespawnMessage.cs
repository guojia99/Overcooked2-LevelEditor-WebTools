using BitStream;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class RespawnMessage : Serialisable
{
	public enum Phase
	{
		Begin = 0,
		End = 1
	}

	public RespawnCollider.RespawnType m_RespawnType;

	public Phase m_Phase;

	public Vector3 m_RespawnPosition = default(Vector3);

	public void Serialise(BitStreamWriter writer)
	{
		writer.Write((uint)m_RespawnType, 4);
		writer.Write((uint)m_Phase, 4);
		if (m_Phase == Phase.End)
		{
			writer.Write(ref m_RespawnPosition);
		}
	}

	public bool Deserialise(BitStreamReader reader)
	{
		m_RespawnType = (RespawnCollider.RespawnType)reader.ReadUInt32(4);
		m_Phase = (Phase)reader.ReadUInt32(4);
		if (m_Phase == Phase.End)
		{
			reader.ReadVector3(ref m_RespawnPosition);
		}
		return true;
	}
}
