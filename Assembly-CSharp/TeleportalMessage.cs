using BitStream;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class TeleportalMessage : Serialisable
{
	public enum MsgType
	{
		PortalState = 0,
		TeleportStart = 1,
		TeleportEnd = 2
	}

	public const int kMsgTypeBits = 2;

	public const int kComponentIndexBits = 8;

	public MsgType m_msgType;

	public bool m_canTeleport;

	public bool m_isTeleporting;

	public ITeleportalSender m_sender;

	public ITeleportalReceiver m_receiver;

	public GameObject m_object;

	public IClientTeleportalSender m_clientSender;

	public IClientTeleportalReceiver m_clientReceiver;

	private EntityMessageHeader m_entityHeader = new EntityMessageHeader();

	public void Initialise_State(bool _canTeleport, bool _isTeleporting)
	{
		m_msgType = MsgType.PortalState;
		m_canTeleport = _canTeleport;
		m_isTeleporting = _isTeleporting;
	}

	public void Initialise_StartTeleport(ITeleportalSender _sender, ITeleportalReceiver _receiver, ITeleportable _object)
	{
		m_msgType = MsgType.TeleportStart;
		m_sender = _sender;
		m_receiver = _receiver;
		m_object = ((MonoBehaviour)_object).gameObject;
	}

	public void Initialise_EndTeleport(ITeleportalReceiver _receiver, ITeleportalSender _sender, ITeleportable _object)
	{
		m_msgType = MsgType.TeleportEnd;
		m_sender = _sender;
		m_receiver = _receiver;
		m_object = ((MonoBehaviour)_object).gameObject;
	}

	public void Serialise(BitStreamWriter writer)
	{
		writer.Write((uint)m_msgType, 2);
		switch (m_msgType)
		{
		case MsgType.PortalState:
			writer.Write(m_canTeleport);
			writer.Write(m_isTeleporting);
			break;
		case MsgType.TeleportStart:
		case MsgType.TeleportEnd:
		{
			SerialiseComponentByIndex<ITeleportalSender>(writer, m_sender as MonoBehaviour);
			SerialiseComponentByIndex<ITeleportalReceiver>(writer, m_receiver as MonoBehaviour);
			EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(m_object);
			entry.m_Header.Serialise(writer);
			break;
		}
		}
	}

	public bool Deserialise(BitStreamReader reader)
	{
		m_msgType = (MsgType)reader.ReadUInt32(2);
		switch (m_msgType)
		{
		case MsgType.PortalState:
			m_canTeleport = reader.ReadBit();
			m_isTeleporting = reader.ReadBit();
			break;
		case MsgType.TeleportStart:
		case MsgType.TeleportEnd:
		{
			m_clientSender = DeserialiseComponentByIndex<IClientTeleportalSender>(reader);
			m_clientReceiver = DeserialiseComponentByIndex<IClientTeleportalReceiver>(reader);
			m_entityHeader.Deserialise(reader);
			EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(m_entityHeader.m_uEntityID);
			m_object = entry.m_GameObject;
			break;
		}
		}
		return true;
	}

	private bool SerialiseComponentByIndex<T>(BitStreamWriter _writer, MonoBehaviour _component) where T : class
	{
		GameObject gameObject = _component.gameObject;
		int bits = gameObject.GetComponents<T>().FindIndex_Predicate((T x) => (object)x == _component);
		EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(gameObject);
		if (entry != null)
		{
			entry.m_Header.Serialise(_writer);
			_writer.Write((uint)bits, 8);
			return true;
		}
		return false;
	}

	private T DeserialiseComponentByIndex<T>(BitStreamReader reader) where T : class
	{
		m_entityHeader.Deserialise(reader);
		EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(m_entityHeader.m_uEntityID);
		GameObject gameObject = entry.m_GameObject;
		int index = (int)reader.ReadUInt32(8);
		return gameObject.GetComponents<T>().TryAtIndex(index);
	}
}
