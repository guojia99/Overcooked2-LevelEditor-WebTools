using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

namespace GameModes.Horde
{
	public class ServerHordeTargetCosmeticDecisions : ServerSynchroniserBase
	{
		private HordeTargetCosmeticDecisions m_cosmeticDecisions;

		public override void StartSynchronising(Component synchronisedObject)
		{
			base.StartSynchronising(synchronisedObject);
			m_cosmeticDecisions = (HordeTargetCosmeticDecisions)synchronisedObject;
		}
	}
}
