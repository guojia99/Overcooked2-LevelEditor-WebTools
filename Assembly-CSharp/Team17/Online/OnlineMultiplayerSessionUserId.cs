using System.IO;
using BitStream;

namespace Team17.Online
{
	public class OnlineMultiplayerSessionUserId : SteamOnlineMultiplayerSessionUserId, IOnlineMultiplayerSessionUserId
	{
		public static readonly byte c_InvalidUniqueId;

		public string m_displayName = string.Empty;

		public bool m_isHost;

		public byte m_uniqueId = c_InvalidUniqueId;

		public bool m_isLocal;

		public bool m_isLocallyMuted;

		public bool m_muteStatusChanged;

		private bool m_isLocallySpeaking;

		private float m_locallySpeakingStopGameTime;

		public string DisplayName
		{
			get
			{
				return m_displayName;
			}
		}

		public bool IsHost
		{
			get
			{
				return m_isHost;
			}
		}

		public bool IsLocal
		{
			get
			{
				return m_isLocal;
			}
		}

		public byte UniqueId
		{
			get
			{
				return m_uniqueId;
			}
		}

		public bool IsLocallyMuted
		{
			get
			{
				return m_isLocallyMuted;
			}
			set
			{
				if (!IsLocal)
				{
					m_isLocallyMuted = value;
					m_muteStatusChanged = true;
				}
			}
		}

		public bool IsSpeaking
		{
			get
			{
				return m_isLocallySpeaking;
			}
		}

		public void Serialize(BitStreamWriter stream)
		{
			using (MemoryStream memoryStream = new MemoryStream())
			{
				using (BinaryWriter binaryWriter = new BinaryWriter(memoryStream))
				{
					binaryWriter.Write(m_displayName);
					binaryWriter.Write(m_isHost);
					binaryWriter.Write(m_uniqueId);
					Write(binaryWriter);
				}
				byte[] array = memoryStream.ToArray();
				stream.Write((uint)array.Length, 16);
				stream.Write(array, array.Length * 8);
			}
		}

		public void Deserialize(BitStreamReader stream)
		{
			uint num = stream.ReadUInt32(16);
			byte[] array = new byte[num];
			for (int i = 0; i < num; i++)
			{
				array[i] = stream.ReadByte(8);
			}
			using (MemoryStream input = new MemoryStream(array))
			{
				using (BinaryReader binaryReader = new BinaryReader(input))
				{
					m_displayName = binaryReader.ReadString();
					m_isHost = binaryReader.ReadBoolean();
					m_uniqueId = binaryReader.ReadByte();
					Read(binaryReader);
				}
			}
		}

		public void SetLocallySpeaking(float gameTimeAtStartOfFrame, float smoothTime = 2f)
		{
			m_locallySpeakingStopGameTime = gameTimeAtStartOfFrame + smoothTime;
			m_isLocallySpeaking = true;
		}

		public void UpdateLocallySpeaking(float gameTimeAtStartOfFrame)
		{
			if (m_isLocallySpeaking && gameTimeAtStartOfFrame > m_locallySpeakingStopGameTime)
			{
				m_isLocallySpeaking = false;
				m_locallySpeakingStopGameTime = 0f;
			}
		}
	}
}
