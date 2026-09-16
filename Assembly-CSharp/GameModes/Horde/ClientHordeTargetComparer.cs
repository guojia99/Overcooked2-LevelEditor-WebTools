using System.Collections.Generic;
using System.Runtime.InteropServices;
using Team17.Online.Multiplayer.Messaging;

namespace GameModes.Horde
{
	[StructLayout(LayoutKind.Sequential, Size = 1)]
	public struct ClientHordeTargetComparer : IComparer<ClientHordeTarget>
	{
		public int Compare(ClientHordeTarget x, ClientHordeTarget y)
		{
			return EntitySerialisationRegistry.GetId(x.gameObject).CompareTo(EntitySerialisationRegistry.GetId(y.gameObject));
		}
	}
}
