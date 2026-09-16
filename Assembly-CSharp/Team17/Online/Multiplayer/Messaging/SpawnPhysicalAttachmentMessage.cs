using BitStream;
using UnityEngine;

namespace Team17.Online.Multiplayer.Messaging
{
	public class SpawnPhysicalAttachmentMessage : Serialisable
	{
		public SpawnEntityMessage m_SpawnEntityData = new SpawnEntityMessage();

		public EntityMessageHeader m_ContainerHeader = new EntityMessageHeader();

		public void Initialise(EntityMessageHeader _spawner, int _spawnableID, EntityMessageHeader _desiredHeader, Vector3 _position, Quaternion _rotation, EntityMessageHeader _container)
		{
			m_SpawnEntityData.Initialise(_spawner, _spawnableID, _desiredHeader, _position, _rotation);
			m_ContainerHeader = _container;
		}

		public void Serialise(BitStreamWriter writer)
		{
			m_SpawnEntityData.Serialise(writer);
			m_ContainerHeader.Serialise(writer);
		}

		public bool Deserialise(BitStreamReader reader)
		{
			bool flag = m_SpawnEntityData.Deserialise(reader);
			return flag | m_ContainerHeader.Deserialise(reader);
		}
	}
}
