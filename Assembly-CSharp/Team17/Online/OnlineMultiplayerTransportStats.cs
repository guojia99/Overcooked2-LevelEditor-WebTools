using System;
using System.Text;

namespace Team17.Online
{
	public class OnlineMultiplayerTransportStats : IOnlineMultiplayerTransportStats
	{
		public enum StatType
		{
			eDataSent = 0,
			eDataReceived = 1,
			eVoiceSent = 2,
			eVoiceReceived = 3
		}

		private StringBuilder m_builder = new StringBuilder(0, 150);

		private string m_builderFailure = "Network Transport Error";

		private float m_gameTimeLast;

		private ulong m_dataBytesSent;

		private ulong m_voiceBytesSent;

		private ulong m_dataBytesReceived;

		private ulong m_voiceBytesReceived;

		private ulong m_dataBytesSentLast;

		private ulong m_voiceBytesSentLast;

		private ulong m_dataBytesReceivedLast;

		private ulong m_voiceBytesReceivedLast;

		public string Text
		{
			get
			{
				return BuildText();
			}
		}

		public void Update(float gameTime)
		{
			if (gameTime - m_gameTimeLast >= 1f)
			{
				m_gameTimeLast = gameTime;
				m_dataBytesSentLast = m_dataBytesSent;
				m_voiceBytesSentLast = m_voiceBytesSent;
				m_dataBytesReceivedLast = m_dataBytesReceived;
				m_voiceBytesReceivedLast = m_voiceBytesReceived;
				m_dataBytesSent = 0uL;
				m_voiceBytesSent = 0uL;
				m_dataBytesReceived = 0uL;
				m_voiceBytesReceived = 0uL;
			}
		}

		public void Add(StatType statType, uint byteValue)
		{
			switch (statType)
			{
			case StatType.eDataSent:
				m_dataBytesSent += byteValue;
				break;
			case StatType.eDataReceived:
				m_dataBytesReceived += byteValue;
				break;
			case StatType.eVoiceSent:
				m_voiceBytesSent += byteValue;
				break;
			case StatType.eVoiceReceived:
				m_voiceBytesReceived += byteValue;
				break;
			}
		}

		public string BuildText()
		{
			try
			{
				m_builder.Length = 0;
				m_builder.Append("Network Transport : Data { ");
				m_builder.Append(m_dataBytesSentLast.ToString());
				m_builder.Append(" : ");
				m_builder.Append(m_dataBytesReceivedLast.ToString());
				m_builder.Append(" }  Voice { ");
				m_builder.Append(m_voiceBytesSentLast.ToString());
				m_builder.Append(" : ");
				m_builder.Append(m_voiceBytesReceivedLast.ToString());
				m_builder.Append(" }");
				return m_builder.ToString();
			}
			catch (Exception)
			{
			}
			return m_builderFailure;
		}
	}
}
