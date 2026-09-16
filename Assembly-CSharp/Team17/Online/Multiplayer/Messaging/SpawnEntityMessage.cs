using BitStream;
using UnityEngine;

namespace Team17.Online.Multiplayer.Messaging
{
	public class SpawnEntityMessage : Serialisable
	{
		public const int kBitsPerSpawnableID = 4;

		public EntityMessageHeader m_SpawnerHeader = new EntityMessageHeader();

		public int m_SpawnableID;

		public EntityMessageHeader m_DesiredHeader = new EntityMessageHeader();

		public Vector3 m_Position;

		public Quaternion m_Rotation;

		public void Initialise(EntityMessageHeader _spawner, int _spawnableID, EntityMessageHeader _desiredHeader, Vector3 _position, Quaternion _rotation)
		{
			m_SpawnerHeader = _spawner;
			m_SpawnableID = _spawnableID;
			m_DesiredHeader = _desiredHeader;
			m_Position = _position;
			m_Rotation = _rotation;
		}

		public void Serialise(BitStreamWriter writer)
		{
			m_SpawnerHeader.Serialise(writer);
			writer.Write((uint)m_SpawnableID, 4);
			m_DesiredHeader.Serialise(writer);
			writer.Write(ref m_Position);
			writer.Write(ref m_Rotation);
		}

		public bool Deserialise(BitStreamReader reader)
		{
			if (m_SpawnerHeader.Deserialise(reader))
			{
				m_SpawnableID = (int)reader.ReadUInt32(4);
				if (m_DesiredHeader.Deserialise(reader))
				{
					reader.ReadVector3(ref m_Position);
					reader.ReadQuaternion(ref m_Rotation);
					return true;
				}
			}
			return false;
		}
	}
}
