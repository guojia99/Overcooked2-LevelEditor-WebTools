using System.Collections.Generic;

namespace Team17.Online
{
	public interface IOnlineMultiplayerSessionEnumerateCoordinator
	{
		bool IsIdle();

		bool Start(OnlineMultiplayerLocalUserId localUserId, List<OnlineMultiplayerSessionPropertySearchValue> filterParameters, ushort maxResults, OnlineMultiplayerSessionEnumerateCallback enumerateCallback);

		void Cancel();
	}
}
