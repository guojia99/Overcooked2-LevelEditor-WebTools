using System.Collections.Generic;

namespace Team17.Online.Multiplayer.Messaging
{
	public class MessageTypeComparer : IEqualityComparer<MessageType>
	{
		public bool Equals(MessageType x, MessageType y)
		{
			return x == y;
		}

		public int GetHashCode(MessageType obj)
		{
			return (int)obj;
		}
	}
}
