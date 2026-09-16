using BitStream;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class InputEventMessage : Serialisable
{
	public enum InputEventType
	{
		Dash = 0,
		DashCollision = 1,
		Catch = 2,
		Curse = 3,
		BeginInteraction = 4,
		EndInteraction = 5,
		TriggerInteraction = 6,
		StartThrow = 7,
		EndThrow = 8
	}

	private InputEventType m_inputEventType;

	public uint entityId;

	public Vector3 collisionContactPoint;

	public InputEventType inputEventType
	{
		get
		{
			return m_inputEventType;
		}
	}

	public InputEventMessage()
	{
	}

	public InputEventMessage(InputEventType inputEventType)
	{
		m_inputEventType = inputEventType;
	}

	public void Serialise(BitStreamWriter writer)
	{
		writer.Write((uint)m_inputEventType, 10);
		writer.Write(entityId, 10);
		if (m_inputEventType == InputEventType.DashCollision)
		{
			writer.Write(collisionContactPoint.x);
			writer.Write(collisionContactPoint.y);
			writer.Write(collisionContactPoint.z);
		}
	}

	public bool Deserialise(BitStreamReader reader)
	{
		m_inputEventType = (InputEventType)reader.ReadUInt32(10);
		entityId = reader.ReadUInt32(10);
		if (m_inputEventType == InputEventType.DashCollision)
		{
			collisionContactPoint.x = reader.ReadFloat32();
			collisionContactPoint.y = reader.ReadFloat32();
			collisionContactPoint.z = reader.ReadFloat32();
		}
		return true;
	}
}
