using System;
using System.Collections.Generic;

namespace Team17.Online
{
	public interface IOnlineFriendsCoordinator
	{
		List<OnlineFriend> DEBUG_GetFriends(GamepadUser localUser);

		List<OnlineFriend> GetFriends(GamepadUser localUser);

		int GetProfileImage(OnlineFriend onlineFriend, IntPtr unmanagedWorkBuffer, int unmanagedWorkBufferSize);

		bool Join(GamepadUser localUser, OnlineFriend friendToJoin);
	}
}
