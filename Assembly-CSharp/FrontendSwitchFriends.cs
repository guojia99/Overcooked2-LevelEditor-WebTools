using System;
using System.Collections.Generic;
using Team17.Online;
using UnityEngine;
using UnityEngine.UI;

public class FrontendSwitchFriends : FrontendMenuBehaviour
{
	private IOnlineFriendsCoordinator m_friendsCoordinator;

	[SerializeField]
	private GameObject m_containerObject;

	[SerializeField]
	private GameObject m_entryPrefab;

	[SerializeField]
	private GameObject m_noEntriesMessage;

	[SerializeField]
	private T17ScrollView m_scrollView;

	private List<GameObject> m_entrys = new List<GameObject>();

	private bool m_hideInvoker;

	public override bool Show(GamepadUser currentGamer, BaseMenuBehaviour parent, GameObject invoker, bool hideInvoker = true)
	{
		if (!base.Show(currentGamer, parent, invoker, hideInvoker))
		{
			return false;
		}
		IOnlinePlatformManager onlinePlatformManager = GameUtils.RequireManagerInterface<IOnlinePlatformManager>();
		m_friendsCoordinator = onlinePlatformManager.OnlineFriendsCoordinator();
		m_hideInvoker = hideInvoker;
		Open(currentGamer);
		if (T17FrontendFlow.Instance != null)
		{
			T17FrontendFlow.Instance.BlockFocusKitchen = true;
		}
		return true;
	}

	public override bool Hide(bool restoreInvokerState = true, bool isTabSwitch = false)
	{
		Clear();
		if (T17FrontendFlow.Instance != null)
		{
			T17FrontendFlow.Instance.BlockFocusKitchen = false;
		}
		m_scrollView.Hide();
		return base.Hide(restoreInvokerState, isTabSwitch);
	}

	public void OnCancel()
	{
		Close();
		if (T17FrontendFlow.Instance != null && T17FrontendFlow.Instance.m_PlayerLobby != null)
		{
			T17FrontendFlow.Instance.m_PlayerLobby.ShowPlayWithFriendsMenu();
		}
	}

	public override void Close()
	{
		base.Close();
	}

	public void Clear()
	{
		m_scrollView.ClearContents();
		m_entrys.Clear();
		if (m_noEntriesMessage != null)
		{
			m_noEntriesMessage.SetActive(false);
		}
	}

	public void Open(GamepadUser currentGamer)
	{
		Enumerate();
		UpdateResultsList();
	}

	public void Refresh()
	{
		Clear();
		Enumerate();
		UpdateResultsList();
	}

	private void Enumerate()
	{
		List<OnlineFriend> list = DEBUG_GetFriends(base.CurrentGamepadUser);
		list.Sort(delegate(OnlineFriend x, OnlineFriend y)
		{
			if (x.m_status == y.m_status)
			{
				return x.m_displayName.CompareTo(y.m_displayName);
			}
			return ((x.m_status == OnlineFriend.FriendStatus.eOnlineInSameApplication && y.m_status == OnlineFriend.FriendStatus.eOnline) || (x.m_status == OnlineFriend.FriendStatus.eOnline && y.m_status == OnlineFriend.FriendStatus.eOnlineInSameApplication)) ? x.m_displayName.CompareTo(y.m_displayName) : (x.m_status.CompareTo(y.m_status) * -1);
		});
		for (int num = 0; num < list.Count; num++)
		{
			GameObject gameObject = UnityEngine.Object.Instantiate(m_entryPrefab, m_containerObject.transform);
			m_entrys.Add(gameObject);
			m_scrollView.AddNewObject(gameObject);
			if (!(gameObject != null) || !(gameObject.transform != null))
			{
				continue;
			}
			FrontendFriendsJoin component = gameObject.GetComponent<FrontendFriendsJoin>();
			if (!(component != null))
			{
				continue;
			}
			component.SetLocalUser(base.CurrentGamepadUser);
			component.SetSelectedFriend(list[num]);
			component.onFriendSelected = OnFriendSelected;
			component.SetName(list[num].m_displayName);
			T17Button t17Button = gameObject.RequestComponent<T17Button>();
			Navigation navigation = t17Button.navigation;
			navigation.selectOnLeft = null;
			navigation.selectOnRight = null;
			if (num == 0)
			{
				navigation.selectOnUp = null;
			}
			t17Button.navigation = navigation;
			component.SetStatus(list[num].m_status);
			if (list[num].m_status == OnlineFriend.FriendStatus.eOnlineInSameApplicationAndJoinable)
			{
				if (t17Button != null)
				{
					t17Button.SubmitAudioTag = GameOneShotAudioTag.UISelect;
				}
			}
			else if (t17Button != null)
			{
				t17Button.SubmitAudioTag = GameOneShotAudioTag.UIBack;
			}
		}
	}

	private void UpdateResultsList()
	{
		if (m_entrys.Count > 0)
		{
			m_scrollView.Show(base.CurrentGamepadUser, m_Parent, m_ObjectThatInvokedShow, m_hideInvoker);
		}
		else
		{
			m_scrollView.Hide();
			if (m_CachedEventSystem != null && m_BorderSelectables.selectOnRight != null)
			{
				m_CachedEventSystem.SetSelectedGameObject(m_BorderSelectables.selectOnRight.gameObject);
			}
		}
		if (m_noEntriesMessage != null)
		{
			m_noEntriesMessage.SetActive(m_entrys.Count == 0);
		}
	}

	private void OnFriendSelected(OnlineFriend friend)
	{
		Close();
	}

	public List<OnlineFriend> DEBUG_GetFriends(GamepadUser localUser)
	{
		if (null != localUser)
		{
			try
			{
				int num = 100;
				List<OnlineFriend> list = new List<OnlineFriend>(num);
				for (int i = 0; i < num; i++)
				{
					OnlineFriend onlineFriend = new OnlineFriend();
					onlineFriend.m_displayName = "test" + UnityEngine.Random.Range(5f, 32f);
					if (i <= 3)
					{
						onlineFriend.m_status = OnlineFriend.FriendStatus.eOnlineInSameApplicationAndJoinable;
					}
					else if (i <= 6)
					{
						onlineFriend.m_status = OnlineFriend.FriendStatus.eOnlineInSameApplication;
					}
					else if (i <= 9)
					{
						onlineFriend.m_status = OnlineFriend.FriendStatus.eOnline;
					}
					else
					{
						onlineFriend.m_status = OnlineFriend.FriendStatus.eOffline;
					}
					list.Add(onlineFriend);
				}
				return list;
			}
			catch (Exception)
			{
			}
		}
		return null;
	}
}
