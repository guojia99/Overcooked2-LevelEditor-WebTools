using Team17.Online;
using UnityEngine;

public class FrontendFriendsJoin : MonoBehaviour
{
	public delegate void OnSelectedDelegate(OnlineFriend friend);

	private IOnlineFriendsCoordinator m_friendsCoordinator;

	[SerializeField]
	private T17Text m_name;

	[SerializeField]
	private T17Text m_StatusText;

	private OnlineFriend m_selectedFriend;

	private GamepadUser m_localUser;

	public OnSelectedDelegate onFriendSelected;

	private static readonly string s_OnlineText = "LobbyScreen.Friends.Online";

	private static readonly string s_OfflineText = "LobbyScreen.Friends.Offline";

	private static readonly string s_JoinableText = "LobbyScreen.Friends.Joinable";

	private void Start()
	{
		IOnlinePlatformManager onlinePlatformManager = GameUtils.RequireManagerInterface<IOnlinePlatformManager>();
		m_friendsCoordinator = onlinePlatformManager.OnlineFriendsCoordinator();
	}

	public void SetName(string name)
	{
		if (m_name != null)
		{
			m_name.text = name;
		}
	}

	public void SetStatus(OnlineFriend.FriendStatus status)
	{
		if (m_StatusText != null)
		{
			switch (status)
			{
			case OnlineFriend.FriendStatus.eOffline:
				m_StatusText.SetLocalisedTextCatchAll(s_OfflineText);
				break;
			case OnlineFriend.FriendStatus.eOnline:
			case OnlineFriend.FriendStatus.eOnlineInSameApplication:
				m_StatusText.SetLocalisedTextCatchAll(s_OnlineText);
				break;
			case OnlineFriend.FriendStatus.eOnlineInSameApplicationAndJoinable:
				m_StatusText.SetLocalisedTextCatchAll(s_JoinableText);
				break;
			}
		}
	}

	public void SetSelectedFriend(OnlineFriend friend)
	{
		m_selectedFriend = friend;
	}

	public void SetLocalUser(GamepadUser localUser)
	{
		m_localUser = localUser;
	}

	public void Join()
	{
		if (m_friendsCoordinator.Join(m_localUser, m_selectedFriend) && onFriendSelected != null)
		{
			onFriendSelected(m_selectedFriend);
		}
	}
}
