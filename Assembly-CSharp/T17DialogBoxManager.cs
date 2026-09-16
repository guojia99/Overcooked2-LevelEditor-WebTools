using System.Collections.Generic;
using UnityEngine;

public class T17DialogBoxManager : MonoBehaviour
{
	public class DialogQueueElement
	{
		public T17DialogBox Dialog;

		public AllowedToShowEvent ShowEvent;
	}

	public delegate void AllowedToShowEvent();

	public static T17DialogBoxManager Instance;

	public GameObject m_DialogBoxPrefab;

	public Canvas m_GlobalDialogBoxCanvas;

	private List<T17DialogBox> m_DialogBoxPool;

	private const int POOL_SIZE = 15;

	private Dictionary<GamepadUser, List<T17DialogBox>> m_Bindings;

	private FastList<GamepadUser> m_bindingKeys;

	private List<T17DialogBox> m_GlobalDialogs;

	private Dictionary<string, Suppressor> m_suppressors = new Dictionary<string, Suppressor>();

	private bool m_bHasGlobalDiags;

	private List<DialogQueueElement> m_DialogBoxQueue = new List<DialogQueueElement>();

	private GameObject m_EventSystem;

	private void Awake()
	{
		if (Instance != null)
		{
			Object.Destroy(this);
			return;
		}
		Instance = this;
		if (m_DialogBoxPrefab != null)
		{
			m_DialogBoxPool = new List<T17DialogBox>();
			m_Bindings = new Dictionary<GamepadUser, List<T17DialogBox>>();
			m_bindingKeys = new FastList<GamepadUser>();
			m_GlobalDialogs = new List<T17DialogBox>();
			for (int i = 0; i < 15; i++)
			{
				GameObject gameObject = Object.Instantiate(m_DialogBoxPrefab);
				gameObject.transform.SetParent(m_GlobalDialogBoxCanvas.transform, false);
				gameObject.transform.localScale = Vector3.one;
				gameObject.transform.localPosition = Vector3.zero;
				T17DialogBox component = gameObject.GetComponent<T17DialogBox>();
				if (component != null)
				{
					gameObject.SetActive(false);
					m_DialogBoxPool.Add(component);
				}
			}
		}
		m_EventSystem = GameObject.Find("EventSystems");
	}

	private void OnDestroy()
	{
		if (Instance == this)
		{
			Instance = null;
		}
	}

	public static T17DialogBox GetDialog(bool forSingleUser)
	{
		if (Instance == null)
		{
			return null;
		}
		forSingleUser = false;
		for (int i = 0; i < 15; i++)
		{
			T17DialogBox t17DialogBox = Instance.m_DialogBoxPool[i];
			if (t17DialogBox.IsActive)
			{
				continue;
			}
			GameObject gameObject = null;
			PlayerManager playerManager = GameUtils.RequireManager<PlayerManager>();
			GamepadUser gamepadUser = playerManager.GetUser(EngagementSlot.One);
			if (gamepadUser == null)
			{
				gamepadUser = playerManager.GetCachedPrimaryUser();
			}
			if (gamepadUser != null)
			{
				if (forSingleUser)
				{
					if (Instance.m_Bindings.ContainsKey(gamepadUser))
					{
						Instance.m_Bindings[gamepadUser].Add(t17DialogBox);
					}
					else
					{
						Instance.m_Bindings.Add(gamepadUser, new List<T17DialogBox> { t17DialogBox });
						Instance.m_bindingKeys.Add(gamepadUser);
					}
				}
				else
				{
					T17EventSystemsManager.Instance.DisableAllEventSystemsExceptFor(gamepadUser);
					gameObject = Instance.m_GlobalDialogBoxCanvas.gameObject;
					Instance.m_GlobalDialogs.Add(t17DialogBox);
					Instance.m_bHasGlobalDiags = true;
				}
				gameObject.SetActive(true);
				t17DialogBox.transform.SetParent(gameObject.transform, false);
				t17DialogBox.transform.localScale = Vector3.one;
				t17DialogBox.transform.localPosition = Vector3.zero;
				t17DialogBox.ReparentToOnHide(Instance.m_GlobalDialogBoxCanvas.transform);
				t17DialogBox.SetEventSystem(T17EventSystemsManager.Instance.GetEventSystemForGamepadUser(gamepadUser));
				return t17DialogBox;
			}
			return null;
		}
		return null;
	}

	public static void RequestDialogShow(T17DialogBox dialog, AllowedToShowEvent callOnAllowed)
	{
		if (!(Instance == null))
		{
			if (Instance.m_EventSystem != null)
			{
				Instance.m_EventSystem.SetActive(true);
			}
			DialogQueueElement dialogQueueElement = new DialogQueueElement();
			dialogQueueElement.Dialog = dialog;
			dialogQueueElement.ShowEvent = callOnAllowed;
			if (!Instance.m_DialogBoxQueue.Exists((DialogQueueElement q) => q.Dialog == dialog))
			{
				Instance.m_DialogBoxQueue.Add(dialogQueueElement);
			}
		}
	}

	public static bool HasGlobalDialogs()
	{
		if (Instance == null)
		{
			return false;
		}
		return Instance.m_bHasGlobalDiags;
	}

	public static bool HasDialogsForGamer(GamepadUser user)
	{
		if (user == null)
		{
			return false;
		}
		if (Instance == null)
		{
			return false;
		}
		if (Instance.m_bHasGlobalDiags)
		{
			return true;
		}
		return Instance.m_Bindings.ContainsKey(user) && Instance.m_Bindings[user].Count > 0;
	}

	public static bool HasAnyOpenDialogs()
	{
		if (Instance == null)
		{
			return false;
		}
		for (int i = 0; i < 15; i++)
		{
			if (Instance.m_DialogBoxPool[i].IsActive)
			{
				return true;
			}
		}
		return false;
	}

	private void EnsureFocus()
	{
		if (Instance.m_bHasGlobalDiags)
		{
			if (m_GlobalDialogs != null && m_GlobalDialogs.Count > 0)
			{
				T17DialogBox t17DialogBox = m_GlobalDialogs[m_GlobalDialogs.Count - 1];
				if (!t17DialogBox.HasFocus())
				{
					t17DialogBox.Focus();
					UpdateHoldsForFocus(t17DialogBox);
				}
			}
			return;
		}
		for (int i = 0; i < Instance.m_bindingKeys.Count; i++)
		{
			GamepadUser key = Instance.m_bindingKeys._items[i];
			List<T17DialogBox> list = Instance.m_Bindings[key];
			if (list != null && list.Count > 0)
			{
				T17DialogBox t17DialogBox2 = list[list.Count - 1];
				if (!t17DialogBox2.HasFocus())
				{
					t17DialogBox2.Focus();
					UpdateHoldsForFocus(t17DialogBox2);
				}
			}
		}
	}

	public void Update()
	{
		if (m_DialogBoxQueue.Count > 0)
		{
			DialogQueueElement dialogQueueElement = m_DialogBoxQueue[0];
			m_DialogBoxQueue.RemoveAt(0);
			dialogQueueElement.ShowEvent();
			UpdateHoldsForFocus(dialogQueueElement.Dialog);
		}
		EnsureFocus();
	}

	private void UpdateHoldsForFocus(T17DialogBox _dialog)
	{
		if (!_dialog.gameObject.activeInHierarchy || !_dialog.IsActive || _dialog.m_EventSystemForGamer == null)
		{
			return;
		}
		T17EventSystem eventSystemForGamer = _dialog.m_EventSystemForGamer;
		if (eventSystemForGamer.AssignedGamepadUser == null)
		{
			return;
		}
		if (!_dialog.HasButtons())
		{
			GamepadUser assignedGamepadUser = eventSystemForGamer.AssignedGamepadUser;
			Suppressor value;
			if (m_suppressors.TryGetValue(assignedGamepadUser.UID, out value))
			{
				if (!value.IsSuppressedBy(_dialog))
				{
					ReleaseHoldForUser(assignedGamepadUser);
					m_suppressors.Add(assignedGamepadUser.UID, eventSystemForGamer.Disable(_dialog));
				}
			}
			else
			{
				m_suppressors.Add(assignedGamepadUser.UID, eventSystemForGamer.Disable(_dialog));
			}
		}
		else
		{
			ReleaseHoldForUser(eventSystemForGamer.AssignedGamepadUser);
		}
	}

	private void ReleaseHoldForUser(GamepadUser _user)
	{
		if (m_suppressors.ContainsKey(_user.UID))
		{
			T17EventSystem eventSystemForGamepadUser = T17EventSystemsManager.Instance.GetEventSystemForGamepadUser(_user);
			if (eventSystemForGamepadUser != null)
			{
				eventSystemForGamepadUser.ReleaseSuppressor(m_suppressors[_user.UID]);
			}
			else
			{
				m_suppressors[_user.UID].Release();
			}
			m_suppressors.Remove(_user.UID);
		}
	}

	public static void ReleaseAll()
	{
		if (Instance == null)
		{
			return;
		}
		for (int i = 0; i < 15; i++)
		{
			if (Instance.m_DialogBoxPool[i].IsActive)
			{
				Instance.m_DialogBoxPool[i].Hide();
			}
		}
		Instance.m_DialogBoxQueue.Clear();
		Instance.m_bHasGlobalDiags = false;
		Instance.m_GlobalDialogs.Clear();
		Instance.m_Bindings.Clear();
		Instance.m_bindingKeys.Clear();
	}

	public static void ReleaseMe(T17DialogBox box)
	{
		int num = Instance.m_DialogBoxQueue.Count - 1;
		for (int num2 = num; num2 >= 0; num2--)
		{
			if (Instance.m_DialogBoxQueue[num2].Dialog == box)
			{
				Instance.m_DialogBoxQueue.RemoveAt(num2);
				break;
			}
		}
		GamepadUser assignedGamepadUser = box.m_EventSystemForGamer.AssignedGamepadUser;
		if (assignedGamepadUser != null)
		{
			Instance.ReleaseHoldForUser(assignedGamepadUser);
		}
		if (Instance.m_GlobalDialogs.Contains(box))
		{
			Instance.m_GlobalDialogs.Remove(box);
			Instance.m_bHasGlobalDiags = Instance.m_GlobalDialogs.Count > 0;
			T17EventSystemsManager.Instance.EnableAllEventSystems();
			return;
		}
		foreach (KeyValuePair<GamepadUser, List<T17DialogBox>> binding in Instance.m_Bindings)
		{
			if (binding.Value.Contains(box))
			{
				binding.Value.Remove(box);
				break;
			}
		}
	}
}
