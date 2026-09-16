using BitStream;
using UnityEngine;

namespace Team17.Online.Multiplayer.Messaging
{
	public class PhysicsObjectMessage : Serialisable
	{
		public const int kTrackedChefCount = 4;

		public Vector3 Velocity = default(Vector3);

		public uint ContactCount;

		public uint[] Contacts = new uint[4];

		public Vector3[] RelativePositions = new Vector3[4];

		public Vector3[] ContactVelocitys = new Vector3[4];

		public float[] ContactTimes = new float[4];

		public WorldObjectMessage WorldObject = new WorldObjectMessage();

		public virtual void Serialise(BitStreamWriter writer)
		{
			writer.Write(ref Velocity);
			writer.Write(ContactCount, 3);
			for (int i = 0; i < ContactCount; i++)
			{
				writer.Write(Contacts[i], 10);
				writer.Write(ref RelativePositions[i]);
				writer.Write(ref ContactVelocitys[i]);
				writer.Write(ContactTimes[i]);
			}
			WorldObject.Serialise(writer);
		}

		public virtual bool Deserialise(BitStreamReader reader)
		{
			reader.ReadVector3(ref Velocity);
			ContactCount = reader.ReadUInt32(3);
			for (int i = 0; i < ContactCount; i++)
			{
				Contacts[i] = reader.ReadUInt32(10);
				reader.ReadVector3(ref RelativePositions[i]);
				reader.ReadVector3(ref ContactVelocitys[i]);
				ContactTimes[i] = reader.ReadFloat32();
			}
			return WorldObject.Deserialise(reader);
		}

		public void Copy(PhysicsObjectMessage _other)
		{
			Velocity = _other.Velocity;
			ContactCount = _other.ContactCount;
			for (int i = 0; i < ContactCount; i++)
			{
				Contacts[i] = _other.Contacts[i];
				RelativePositions[i] = _other.RelativePositions[i];
				ContactVelocitys[i] = _other.ContactVelocitys[i];
				ContactTimes[i] = _other.ContactTimes[i];
			}
			WorldObject.Copy(_other.WorldObject);
		}
	}
}
