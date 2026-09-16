using BitStream;
using UnityEngine;

namespace Team17.Online.Multiplayer.Messaging
{
	public class AvatarPositionMessage : Serialisable
	{
		public WorldObjectMessage WorldObject = new WorldObjectMessage();

		public Vector3 Velocity = Vector3.zero;

		public void Serialise(BitStreamWriter writer)
		{
			WorldObject.Serialise(writer);
			writer.Write(Velocity.x);
			writer.Write(Velocity.y);
			writer.Write(Velocity.z);
		}

		public bool Deserialise(BitStreamReader reader)
		{
			WorldObject.Deserialise(reader);
			Velocity.Set(reader.ReadFloat32(), reader.ReadFloat32(), reader.ReadFloat32());
			return true;
		}
	}
}
