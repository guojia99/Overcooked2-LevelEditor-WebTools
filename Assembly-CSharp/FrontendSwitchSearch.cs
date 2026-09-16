using System;
using System.Collections.Generic;
using Team17.Online;
using UnityEngine;

public class FrontendSwitchSearch : FrontendMenuBehaviour
{
	[SerializeField]
	private GameObject m_containerObject;

	[SerializeField]
	private GameObject m_entryPrefab;

	[SerializeField]
	private GameObject m_noEntriesMessage;

	[SerializeField]
	private T17ScrollView m_scrollView;

	private List<GameObject> m_entrys = new List<GameObject>();

	private SearchTask.SearchResultData m_results;

	public void SetResults(SearchTask.SearchResultData results)
	{
		m_results = results;
	}

	public override bool Show(GamepadUser currentGamer, BaseMenuBehaviour parent, GameObject invoker, bool hideInvoker = true)
	{
		if (!base.Show(currentGamer, parent, invoker, hideInvoker))
		{
			return false;
		}
		if (T17FrontendFlow.Instance != null)
		{
			T17FrontendFlow.Instance.BlockFocusKitchen = true;
		}
		if (m_results != null && m_results.m_AvailableSessions != null && m_results.m_AvailableSessions.Count > 0)
		{
			if (m_noEntriesMessage != null)
			{
				m_noEntriesMessage.SetActive(false);
			}
			Open(currentGamer);
			m_scrollView.Show(currentGamer, parent, invoker, hideInvoker);
		}
		else
		{
			if (m_noEntriesMessage != null)
			{
				m_noEntriesMessage.SetActive(true);
			}
			if (m_CachedEventSystem != null && m_BorderSelectables.selectOnRight != null)
			{
				m_CachedEventSystem.SetSelectedGameObject(m_BorderSelectables.selectOnRight.gameObject);
			}
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
		if (m_noEntriesMessage != null)
		{
			m_noEntriesMessage.SetActive(false);
		}
		m_scrollView.Hide();
		return base.Hide(restoreInvokerState, isTabSwitch);
	}

	public void Clear()
	{
		m_scrollView.ClearContents();
		m_entrys.Clear();
	}

	public void Open(GamepadUser currentGamer)
	{
		if (m_results == null || m_results.m_AvailableSessions == null)
		{
			return;
		}
		List<OnlineMultiplayerSessionEnumeratedRoom> availableSessions = m_results.m_AvailableSessions;
		for (int i = 0; i < availableSessions.Count; i++)
		{
			GameObject gameObject = UnityEngine.Object.Instantiate(m_entryPrefab, m_containerObject.transform);
			m_scrollView.AddNewObject(gameObject);
			if (gameObject != null && gameObject.transform != null)
			{
				m_entrys.Add(gameObject);
				FrontendSearchJoin component = gameObject.GetComponent<FrontendSearchJoin>();
				if (component != null)
				{
					component.SetLocalUser(currentGamer);
					component.SetSelectedSession(availableSessions[i]);
					component.SetSessionName(availableSessions[i].GetHostName());
					component.onComplete = (FrontendSearchJoin.onCompleteDelegate)Delegate.Combine(component.onComplete, new FrontendSearchJoin.onCompleteDelegate(Close));
				}
			}
		}
	}

	public void OnCancel()
	{
		Close();
		T17FrontendFlow instance = T17FrontendFlow.Instance;
		if (instance != null && instance.m_PlayerLobby != null)
		{
			instance.m_PlayerLobby.OnSearchCancelled();
		}
	}

	public override void Close()
	{
		base.Close();
		if (T17FrontendFlow.Instance != null)
		{
			T17FrontendFlow.Instance.FocusOnMultiplayerKitchen(true);
		}
	}
}
