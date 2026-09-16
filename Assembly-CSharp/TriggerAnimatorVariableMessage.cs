using BitStream;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class TriggerAnimatorVariableMessage : Serialisable
{
	public enum RandomValueType
	{
		None = 0,
		Bool = 1,
		Int = 2,
		Float = 3
	}

	private RandomValueType m_type;

	private const int m_kBitsPerType = 2;

	public object m_randomValue;

	public RandomValueType Type
	{
		get
		{
			return m_type;
		}
	}

	public void Initialise()
	{
		m_type = RandomValueType.None;
	}

	public void InitRandomFloat(float _min, float _max)
	{
		m_type = RandomValueType.Float;
		m_randomValue = Random.Range(_min, _max);
	}

	public void InitRandomInt(int _min, int _max)
	{
		m_type = RandomValueType.Int;
		m_randomValue = Random.Range(_min, _max);
	}

	public void InitRandomBool()
	{
		m_type = RandomValueType.Bool;
		m_randomValue = Random.Range(0, 1) == 1;
	}

	public void Serialise(BitStreamWriter writer)
	{
		writer.Write((uint)m_type, 2);
		switch (m_type)
		{
		case RandomValueType.Bool:
			writer.Write((bool)m_randomValue);
			break;
		case RandomValueType.Float:
			writer.Write((float)m_randomValue);
			break;
		case RandomValueType.Int:
			writer.Write((int)m_randomValue);
			break;
		}
	}

	public bool Deserialise(BitStreamReader reader)
	{
		m_type = (RandomValueType)reader.ReadUInt32(2);
		switch (m_type)
		{
		case RandomValueType.Bool:
			m_randomValue = reader.ReadBit();
			break;
		case RandomValueType.Float:
			m_randomValue = reader.ReadFloat32();
			break;
		case RandomValueType.Int:
			m_randomValue = (int)reader.ReadFloat32();
			break;
		}
		return true;
	}
}
