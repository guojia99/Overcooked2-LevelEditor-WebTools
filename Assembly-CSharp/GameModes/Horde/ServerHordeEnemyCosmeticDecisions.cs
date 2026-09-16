using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

namespace GameModes.Horde
{
	public class ServerHordeEnemyCosmeticDecisions : ServerSynchroniserBase
	{
		private HordeEnemyCosmeticDecisions m_cosmeticDecisions;

		public override void StartSynchronising(Component synchronisedObject)
		{
			base.StartSynchronising(synchronisedObject);
			m_cosmeticDecisions = (HordeEnemyCosmeticDecisions)synchronisedObject;
		}
	}
}
