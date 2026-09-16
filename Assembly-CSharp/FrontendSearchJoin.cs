using System.Collections.Generic;
using Team17.Online;
using UnityEngine;

public class FrontendSearchJoin : MonoBehaviour
{
	public delegate void onCompleteDelegate();

	[SerializeField]
	private T17Text m_name;

	private OnlineMultiplayerSessionEnumeratedRoom m_selectedSession;

	private GamepadUser m_localUser;

	public onCompleteDelegate onComplete;

	private readonly string c_TitleText = "Text.PleaseWait";

	private readonly string c_InProgressText = "Online.ConnectionMode.JoinSession.InProgress";

	private readonly string c_SuccessText = "Online.ConnectionMode.JoinSession.Result.eSuccess";

	private readonly string c_FailedText = "Online.ConnectionMode.JoinSession.Result.eGenericFailure";

	private void Start()
	{
	}

	public void SetSessionName(string sessionName)
	{
		if (m_name != null)
		{
			m_name.text = sessionName;
		}
	}

	public void SetSelectedSession(OnlineMultiplayerSessionEnumeratedRoom session)
	{
		m_selectedSession = session;
	}

	public void SetLocalUser(GamepadUser localUser)
	{
		m_localUser = localUser;
	}

	public void Join()
	{
		JoinEnumeratedRoomOptions joinEnumeratedRoomOptions = new JoinEnumeratedRoomOptions();
		joinEnumeratedRoomOptions.Room = m_selectedSession;
		joinEnumeratedRoomOptions.User = m_localUser;
		string progressText = "Missing User";
		User user = null;
		FastList<User> users = ClientUserSystem.m_Users;
		for (int i = 0; i < users.Count; i++)
		{
			User user2 = users._items[i];
			if (user2.Engagement == EngagementSlot.One)
			{
				if (user2 != null && user2.GamepadUser != null)
				{
					user = user2;
				}
				break;
			}
		}
		if (user != null)
		{
			progressText = Localization.Get(c_InProgressText, new LocToken("[NAME]", user.DisplayName));
		}
		if (T17FrontendFlow.Instance != null && T17FrontendFlow.Instance.m_PlayerLobby != null)
		{
			T17FrontendFlow.Instance.m_PlayerLobby.JoinAdhocGame(joinEnumeratedRoomOptions, progressText);
		}
		if (onComplete != null)
		{
			onComplete();
		}
	}
}
