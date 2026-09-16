namespace Team17.Online
{
	public interface IOnlineMultiplayerSessionUserId
	{
		string DisplayName { get; }

		bool IsHost { get; }

		bool IsLocal { get; }

		byte UniqueId { get; }

		bool IsLocallyMuted { get; set; }

		bool IsSpeaking { get; }

		OnlineUserPlatformId PlatformUserId { get; }
	}
}
