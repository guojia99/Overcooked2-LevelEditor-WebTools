using System.Collections.Generic;
using BitStream;

namespace Team17.Online.Multiplayer.Messaging
{
	public class SerialisationRegistry<T>
	{
		public static Dictionary<T, Serialisable> s_MessageTypes;

		public static void Initialise(IEqualityComparer<T> comparer)
		{
			s_MessageTypes = new Dictionary<T, Serialisable>(comparer);
		}

		public static void RegisterMessageType(T type, Serialisable message)
		{
			s_MessageTypes[type] = message;
		}

		public static bool Deserialise(out Serialisable message, T type, BitStreamReader reader)
		{
			if (s_MessageTypes.ContainsKey(type))
			{
				message = s_MessageTypes[type];
				return message.Deserialise(reader);
			}
			message = null;
			return false;
		}
	}
}
