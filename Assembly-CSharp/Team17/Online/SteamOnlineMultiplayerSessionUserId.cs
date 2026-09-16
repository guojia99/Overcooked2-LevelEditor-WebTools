using System.IO;
using Steamworks;

namespace Team17.Online
{
	public abstract class SteamOnlineMultiplayerSessionUserId
	{
		public enum TransportConnectionStatus : byte
		{
			eNotApplicable = 0,
			eWaitingForJoinApprovalFromHost = 1,
			eWaitingToStartClientConnection = 2,
			eOpeningTransportConnectionToClientStartDelay = 3,
			eOpeningTransportConnectionToClient = 4,
			eConnectionOpenSendClientHello = 5,
			eConnectionActive = 6,
			eConnectionDead = 7
		}

		public TransportConnectionStatus m_steamLocalTransportConnectionStatus;

		public float m_steamLocalKeepaliveLastSendTime;

		public float m_steamLocalKeepaliveLastReceiveTime;

		public CSteamID m_steamId;

		public uint m_steamUserRestrictions;

		public OnlineUserPlatformId PlatformUserId
		{
			get
			{
				OnlineUserPlatformId onlineUserPlatformId = new OnlineUserPlatformId();
				onlineUserPlatformId.m_steamId = m_steamId;
				return onlineUserPlatformId;
			}
		}

		protected void Write(BinaryWriter writer)
		{
			writer.Write(m_steamId.m_SteamID);
			writer.Write(m_steamUserRestrictions);
		}

		protected void Read(BinaryReader reader)
		{
			m_steamId = new CSteamID(reader.ReadUInt64());
			m_steamUserRestrictions = reader.ReadUInt32();
		}

		public bool HasDirectTransportConnection()
		{
			return m_steamLocalTransportConnectionStatus == TransportConnectionStatus.eConnectionActive && m_steamId.IsValid();
		}
	}
}
