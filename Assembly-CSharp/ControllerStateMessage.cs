using BitStream;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ControllerStateMessage : Serialisable
{
	public uint m_uChefEntityID;

	public byte m_ButtonStates;

	public float m_AxisX;

	public float m_AxisY;

	public float m_Time;

	public bool m_underControl = true;

	public Quaternion rotation;

	public bool Deserialise(BitStreamReader reader)
	{
		m_ButtonStates = reader.ReadByte(4);
		m_AxisX = reader.ReadFloat32();
		m_AxisY = reader.ReadFloat32();
		m_uChefEntityID = reader.ReadUInt16(10);
		m_Time = reader.ReadFloat32();
		reader.ReadQuaternion(ref rotation);
		m_underControl = reader.ReadBit();
		return true;
	}

	public void Serialise(BitStreamWriter writer)
	{
		writer.Write(m_ButtonStates, 4);
		writer.Write(m_AxisX);
		writer.Write(m_AxisY);
		writer.Write(m_uChefEntityID, 10);
		writer.Write(m_Time);
		writer.Write(ref rotation);
		writer.Write(m_underControl);
	}

	public bool IsButtonDown(PlayerInputLookup.LogicalButtonID _button)
	{
		switch (_button)
		{
		case PlayerInputLookup.LogicalButtonID.Dash:
			return (m_ButtonStates & 1) != 0;
		case PlayerInputLookup.LogicalButtonID.Curse:
			return (m_ButtonStates & 2) != 0;
		case PlayerInputLookup.LogicalButtonID.PickupAndDrop:
			return (m_ButtonStates & 4) != 0;
		case PlayerInputLookup.LogicalButtonID.WorkstationInteract:
			return (m_ButtonStates & 8) != 0;
		default:
			return false;
		}
	}

	public static byte GetButtonState(bool _dash, bool _curse, bool _pickup, bool _interact)
	{
		byte b = 0;
		if (_dash)
		{
			b |= 1;
		}
		if (_curse)
		{
			b |= 2;
		}
		if (_pickup)
		{
			b |= 4;
		}
		if (_interact)
		{
			b |= 8;
		}
		return b;
	}

	public void SetButtonPressed(PlayerInputLookup.LogicalButtonID _button, bool _pressed)
	{
		if (_pressed)
		{
			switch (_button)
			{
			case PlayerInputLookup.LogicalButtonID.Dash:
				m_ButtonStates |= 1;
				break;
			case PlayerInputLookup.LogicalButtonID.Curse:
				m_ButtonStates |= 2;
				break;
			case PlayerInputLookup.LogicalButtonID.PickupAndDrop:
				m_ButtonStates |= 4;
				break;
			case PlayerInputLookup.LogicalButtonID.WorkstationInteract:
				m_ButtonStates |= 8;
				break;
			}
		}
		else
		{
			switch (_button)
			{
			case PlayerInputLookup.LogicalButtonID.Dash:
				m_ButtonStates = (byte)(m_ButtonStates & -2);
				break;
			case PlayerInputLookup.LogicalButtonID.Curse:
				m_ButtonStates = (byte)(m_ButtonStates & -3);
				break;
			case PlayerInputLookup.LogicalButtonID.PickupAndDrop:
				m_ButtonStates = (byte)(m_ButtonStates & -5);
				break;
			case PlayerInputLookup.LogicalButtonID.WorkstationInteract:
				m_ButtonStates = (byte)(m_ButtonStates & -9);
				break;
			}
		}
	}

	public void Copy(ControllerStateMessage _other)
	{
		m_ButtonStates = _other.m_ButtonStates;
		m_AxisX = _other.m_AxisX;
		m_AxisY = _other.m_AxisY;
		m_uChefEntityID = _other.m_uChefEntityID;
		m_Time = _other.m_Time;
	}

	public bool IsDifferent(ControllerStateMessage other)
	{
		if ((other == null && this != null) || (this == null && other != null))
		{
			return true;
		}
		if (other == null && this == null)
		{
			return false;
		}
		return other.m_AxisX != m_AxisX || other.m_AxisY != m_AxisY || other.m_ButtonStates != m_ButtonStates || other.m_uChefEntityID != m_uChefEntityID || other.m_Time != m_Time || other.m_underControl != m_underControl;
	}
}
