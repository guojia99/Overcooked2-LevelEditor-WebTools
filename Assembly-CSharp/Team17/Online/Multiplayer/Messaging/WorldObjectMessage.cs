using BitStream;
using UnityEngine;

namespace Team17.Online.Multiplayer.Messaging
{
	public class WorldObjectMessage : Serialisable
	{
		public Vector3 LocalPosition = default(Vector3);

		public Quaternion LocalRotation = default(Quaternion);

		public bool HasParent;

		public bool HasPositions;

		public uint ParentEntityID;

		public virtual void Serialise(BitStreamWriter writer)
		{
			writer.Write(HasParent);
			if (HasParent)
			{
				writer.Write(ParentEntityID, 10);
			}
			writer.Write(HasPositions);
			if (HasPositions)
			{
				writer.Write(ref LocalPosition);
				writer.Write(ref LocalRotation);
			}
		}

		public virtual bool Deserialise(BitStreamReader reader)
		{
			HasParent = reader.ReadBit();
			if (HasParent)
			{
				ParentEntityID = reader.ReadUInt32(10);
			}
			else
			{
				ParentEntityID = 0u;
			}
			HasPositions = reader.ReadBit();
			if (HasPositions)
			{
				reader.ReadVector3(ref LocalPosition);
				reader.ReadQuaternion(ref LocalRotation);
			}
			return true;
		}

		public void Copy(WorldObjectMessage _other)
		{
			LocalPosition = _other.LocalPosition;
			LocalRotation = _other.LocalRotation;
			HasParent = _other.HasParent;
			HasPositions = _other.HasPositions;
			ParentEntityID = _other.ParentEntityID;
		}
	}
}
