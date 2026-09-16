using System.Collections.Generic;

namespace Team17.Online.Multiplayer.Messaging
{
	public class EntityTypeComparer : IEqualityComparer<EntityType>
	{
		public bool Equals(EntityType x, EntityType y)
		{
			return x == y;
		}

		public int GetHashCode(EntityType obj)
		{
			return (int)obj;
		}
	}
}
