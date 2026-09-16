using System;

namespace Team17.Online.Multiplayer.Messaging
{
	public class SynchroniserConfig
	{
		public InstancesPerGameObject m_InstancesAllowed;

		public Type m_ServerSynchroniserType;

		public Type m_ClientSynchroniserType;

		public SynchroniserConfig(InstancesPerGameObject instancesAllowed, Type serverSynchroniserType, Type clientSynchroniserType)
		{
			m_InstancesAllowed = instancesAllowed;
			m_ServerSynchroniserType = serverSynchroniserType;
			m_ClientSynchroniserType = clientSynchroniserType;
		}
	}
}
