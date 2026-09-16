using System.Collections.Generic;
using UnityEngine;

namespace Team17.Online.Multiplayer.Messaging
{
	public class EntitySerialisationEntry
	{
		public int m_iLevelDataID;

		public GameObject m_GameObject;

		public EntityMessageHeader m_Header = new EntityMessageHeader();

		public FastList<ServerSynchroniser> m_ServerSynchronisedComponents = new FastList<ServerSynchroniser>();

		public FastList<ClientSynchroniser> m_ClientSynchronisedComponents = new FastList<ClientSynchroniser>();

		private bool m_bUrgentUpdate;

		public bool HasUrgentUpdate()
		{
			return m_bUrgentUpdate;
		}

		public void SetRequiresUrgentUpdate(bool bUpdate)
		{
			m_bUrgentUpdate = bUpdate;
			if (!EntitySerialisationRegistry.HasUrgentOutgoingUpdates && m_bUrgentUpdate)
			{
				EntitySerialisationRegistry.HasUrgentOutgoingUpdates = true;
			}
		}
	}
}
