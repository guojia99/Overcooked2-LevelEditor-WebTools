using BitStream;
using UnityEngine;

namespace Team17.Online.Multiplayer.Messaging
{
	public class ChefEventMessage : Serialisable
	{
		public enum ChefEventType : byte
		{
			PickUp = 0,
			Place = 1,
			Take = 2,
			Interact = 3,
			TriggerInteract = 4,
			Throw = 5,
			KnockBack = 6,
			COUNT = 7
		}

		public enum KnockbackType
		{
			Throw = 0,
			Fire = 1,
			COUNT = 2
		}

		private static readonly int kChefEventBitCount = GameUtils.GetRequiredBitCount(7);

		private static readonly int kChefEventKnockbackTypeBitCount = GameUtils.GetRequiredBitCount(2);

		public ChefEventType EventType = ChefEventType.COUNT;

		public uint EntityID;

		public uint ChefEntityID;

		public Vector2 KnockbackForce = default(Vector2);

		public Vector3 RelativeContactPoint = default(Vector3);

		public KnockbackType Knockback_Type = KnockbackType.COUNT;

		public void Initialise(ChefEventType _type, uint _chefEntityID, uint _entityID)
		{
			EventType = _type;
			EntityID = _entityID;
			ChefEntityID = _chefEntityID;
		}

		public bool Deserialise(BitStreamReader reader)
		{
			EventType = (ChefEventType)reader.ReadByte(kChefEventBitCount);
			ChefEntityID = reader.ReadUInt32(10);
			EntityID = reader.ReadUInt32(10);
			if (EventType == ChefEventType.KnockBack)
			{
				Knockback_Type = (KnockbackType)reader.ReadUInt32(kChefEventKnockbackTypeBitCount);
				reader.ReadVector2(ref KnockbackForce);
				reader.ReadVector3(ref RelativeContactPoint);
			}
			return true;
		}

		public void Serialise(BitStreamWriter writer)
		{
			writer.Write((byte)EventType, kChefEventBitCount);
			writer.Write(ChefEntityID, 10);
			writer.Write(EntityID, 10);
			if (EventType == ChefEventType.KnockBack)
			{
				writer.Write((uint)Knockback_Type, kChefEventKnockbackTypeBitCount);
				writer.Write(ref KnockbackForce);
				writer.Write(ref RelativeContactPoint);
			}
		}
	}
}
