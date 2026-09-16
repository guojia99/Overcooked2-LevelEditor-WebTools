using BitStream;
using UnityEngine;

namespace Team17.Online.Multiplayer.Messaging
{
	public class ChefEffectMessage : Serialisable
	{
		public enum EffectType
		{
			Impact = 0,
			Dash = 1,
			COUNT = 2
		}

		private static readonly int kEffectTypeBitCount = GameUtils.GetRequiredBitCount(2);

		public uint ChefEntityID;

		public Vector3 RelativePosition;

		public EffectType Effect;

		public void Initalise(uint _ChefEntityID, EffectType _effectType, Vector3 _relativePosition)
		{
			ChefEntityID = _ChefEntityID;
			Effect = _effectType;
			RelativePosition = _relativePosition;
		}

		public bool Deserialise(BitStreamReader _reader)
		{
			ChefEntityID = _reader.ReadUInt32(10);
			_reader.ReadVector3(ref RelativePosition);
			Effect = (EffectType)_reader.ReadUInt32(kEffectTypeBitCount);
			return true;
		}

		public void Serialise(BitStreamWriter _writer)
		{
			_writer.Write(ChefEntityID, 10);
			_writer.Write(ref RelativePosition);
			_writer.Write((uint)Effect, kEffectTypeBitCount);
		}
	}
}
