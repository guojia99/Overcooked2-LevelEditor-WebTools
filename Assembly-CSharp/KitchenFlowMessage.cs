using BitStream;
using OrderController;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class KitchenFlowMessage : Serialisable
{
	public enum MsgType
	{
		Delivery = 0,
		OrderAdded = 1,
		OrderExpired = 2,
		ScoreOnly = 3
	}

	public const int kMsgTypeBits = 2;

	public const int kTeamIDBits = 2;

	public const int kTipBits = 6;

	public MsgType m_msgType;

	public TeamID m_teamID;

	public TeamMonitor.TeamScoreStats m_teamScore = new TeamMonitor.TeamScoreStats();

	public OrderID m_orderID;

	public ServerOrderData m_orderData = new ServerOrderData();

	public GameObject m_plateStation;

	public bool m_success;

	public bool m_wasCombo;

	public int m_tip;

	public float m_timePropRemainingPercentage;

	private EntityMessageHeader m_plateStationHeader = new EntityMessageHeader();

	public void SetScoreData(TeamMonitor.TeamScoreStats _scoreData)
	{
		m_teamScore.Copy(_scoreData);
	}

	public void Initialise_DeliverySuccess(TeamID _teamID, GameObject _station, OrderID _orderID, float _timePropRemainingPercentage, int _tip, bool _wasCombo)
	{
		m_msgType = MsgType.Delivery;
		m_teamID = _teamID;
		m_success = true;
		m_plateStation = _station;
		m_orderID = _orderID;
		m_wasCombo = _wasCombo;
		m_timePropRemainingPercentage = _timePropRemainingPercentage;
		m_tip = _tip;
	}

	public void Initialise_DeliveryFailed(TeamID _teamID, GameObject _station)
	{
		m_msgType = MsgType.Delivery;
		m_teamID = _teamID;
		m_success = false;
		m_plateStation = _station;
	}

	public void Initialise_OrderAdded(TeamID _teamID, Serialisable _orderData)
	{
		m_msgType = MsgType.OrderAdded;
		m_teamID = _teamID;
		m_orderData = (ServerOrderData)_orderData;
	}

	public void Initialise_OrderExpired(TeamID _teamID, OrderID _orderID)
	{
		m_msgType = MsgType.OrderExpired;
		m_teamID = _teamID;
		m_orderID = _orderID;
	}

	public void Initialise_ScoreOnly(TeamID _teamID)
	{
		m_msgType = MsgType.ScoreOnly;
		m_teamID = _teamID;
	}

	public void Serialise(BitStreamWriter writer)
	{
		writer.Write((uint)m_msgType, 2);
		writer.Write((uint)m_teamID, 2);
		switch (m_msgType)
		{
		case MsgType.Delivery:
			writer.Write(m_success);
			m_teamScore.Serialise(writer);
			if (m_success)
			{
				EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(m_plateStation);
				entry.m_Header.Serialise(writer);
				m_orderID.Serialise(writer);
				writer.Write(m_wasCombo);
				writer.Write(m_timePropRemainingPercentage);
				writer.Write((uint)m_tip, 6);
			}
			break;
		case MsgType.OrderAdded:
			m_orderData.Serialise(writer);
			break;
		case MsgType.OrderExpired:
			m_orderID.Serialise(writer);
			m_teamScore.Serialise(writer);
			break;
		case MsgType.ScoreOnly:
			m_teamScore.Serialise(writer);
			break;
		}
	}

	public bool Deserialise(BitStreamReader reader)
	{
		m_msgType = (MsgType)reader.ReadUInt32(2);
		m_teamID = (TeamID)reader.ReadUInt32(2);
		switch (m_msgType)
		{
		case MsgType.Delivery:
			m_success = reader.ReadBit();
			m_teamScore.Deserialise(reader);
			if (m_success)
			{
				m_plateStationHeader.Deserialise(reader);
				EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(m_plateStationHeader.m_uEntityID);
				if (entry == null)
				{
					return false;
				}
				m_plateStation = entry.m_GameObject;
				m_orderID.Deserialise(reader);
				m_wasCombo = reader.ReadBit();
				m_timePropRemainingPercentage = reader.ReadFloat32();
				m_tip = (int)reader.ReadUInt32(6);
			}
			break;
		case MsgType.OrderAdded:
			m_orderData.Deserialise(reader);
			break;
		case MsgType.OrderExpired:
			m_orderID.Deserialise(reader);
			m_teamScore.Deserialise(reader);
			break;
		case MsgType.ScoreOnly:
			m_teamScore.Deserialise(reader);
			break;
		}
		return true;
	}
}
