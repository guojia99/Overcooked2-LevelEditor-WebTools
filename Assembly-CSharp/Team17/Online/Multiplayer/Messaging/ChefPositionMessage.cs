using BitStream;
using UnityEngine;

namespace Team17.Online.Multiplayer.Messaging
{
	public class ChefPositionMessage : Serialisable
	{
		public WorldObjectMessage WorldObject = new WorldObjectMessage();

		public Vector3 Velocity = Vector3.zero;

		public float NetworkTime;

		public float ClientTimeStamp;

		public void Serialise(BitStreamWriter writer)
		{
			WorldObject.Serialise(writer);
			writer.Write(Velocity.x);
			writer.Write(Velocity.y);
			writer.Write(Velocity.z);
			writer.Write(NetworkTime);
			writer.Write(ClientTimeStamp);
		}

		public bool Deserialise(BitStreamReader reader)
		{
			WorldObject.Deserialise(reader);
			Velocity.Set(reader.ReadFloat32(), reader.ReadFloat32(), reader.ReadFloat32());
			NetworkTime = reader.ReadFloat32();
			ClientTimeStamp = reader.ReadFloat32();
			return true;
		}

		public void Copy(ChefPositionMessage _other)
		{
			WorldObject.Copy(_other.WorldObject);
			Velocity = _other.Velocity;
			NetworkTime = _other.NetworkTime;
			ClientTimeStamp = _other.ClientTimeStamp;
		}
	}
}
