using BitStream;
using Team17.Online;
using Team17.Online.Multiplayer.Messaging;

public class GameStateMessage : Serialisable
{
	public enum PayLoadType
	{
		ClientSave = 0,
		COUNT = 1
	}

	public abstract class GameStatePayload : Serialisable
	{
		public abstract PayLoadType GetPayLoadType();

		public abstract void Serialise(BitStreamWriter writer);

		public abstract bool Deserialise(BitStreamReader reader);
	}

	public class ClientSavePayload : GameStatePayload
	{
		public int DLCID { get; private set; }

		public override PayLoadType GetPayLoadType()
		{
			return PayLoadType.ClientSave;
		}

		public void Initialise(int dlcID)
		{
			DLCID = dlcID;
		}

		public override void Serialise(BitStreamWriter writer)
		{
			bool flag = DLCID != -1;
			writer.Write(flag);
			if (flag)
			{
				writer.Write((uint)DLCID, 4);
			}
		}

		public override bool Deserialise(BitStreamReader reader)
		{
			if (reader.ReadBit())
			{
				DLCID = (int)reader.ReadUInt32(4);
			}
			else
			{
				DLCID = -1;
			}
			return true;
		}
	}

	public const int kGameStateBits = 6;

	public GameState m_State;

	public User.MachineID m_Machine = User.MachineID.Count;

	private int kBitsPayloadType = GameUtils.GetRequiredBitCount(1);

	public GameStatePayload Payload { get; private set; }

	public void Initialise(GameState state, User.MachineID machine)
	{
		Initialise(state, machine, null);
	}

	public void Initialise(GameState state, User.MachineID machine, GameStatePayload payload)
	{
		m_State = state;
		m_Machine = machine;
		Payload = payload;
	}

	public void Serialise(BitStreamWriter writer)
	{
		writer.Write((uint)m_State, 6);
		writer.Write((uint)m_Machine, 3);
		writer.Write(Payload != null);
		if (Payload != null)
		{
			writer.Write((uint)Payload.GetPayLoadType(), kBitsPayloadType);
			Payload.Serialise(writer);
		}
	}

	public bool Deserialise(BitStreamReader reader)
	{
		m_State = (GameState)reader.ReadUInt32(6);
		m_Machine = (User.MachineID)reader.ReadUInt32(3);
		if (reader.ReadBit())
		{
			GameStatePayload payloadForType = GetPayloadForType((PayLoadType)reader.ReadUInt32(kBitsPayloadType));
			payloadForType.Deserialise(reader);
			Payload = payloadForType;
		}
		else
		{
			Payload = null;
		}
		return true;
	}

	private GameStatePayload GetPayloadForType(PayLoadType type)
	{
		if (type == PayLoadType.ClientSave)
		{
			return new ClientSavePayload();
		}
		return null;
	}
}
