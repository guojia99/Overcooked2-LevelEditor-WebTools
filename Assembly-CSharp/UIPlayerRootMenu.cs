using System;
using System.Collections.Generic;
using Team17.Online;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIPlayerRootMenu : RootMenu
{
	private struct SelectionCache
	{
		public bool m_hasCached;

		public string m_selectedUserName;

		public int m_selectedIdx;

		public UIPlayerMenuBehaviour.UIPlayerMenuOptions? m_selectedOption;

		public void Clear()
		{
			m_hasCached = false;
			m_selectedUserName = null;
			m_selectedIdx = -1;
			m_selectedOption = null;
		}
	}

	[SerializeField]
	[AssignResource("UIPlayer", Editorbility.Editable)]
	private UIPlayerMenuBehaviour m_uiPlayerPrefab;

	private List<UIPlayerMenuBehaviour> m_players = new List<UIPlayerMenuBehaviour>();

	[SerializeField]
	[AssignChildRecursive("Background", Editorbility.Editable)]
	public Transform m_backgroundsContainer;

	[SerializeField]
	[AssignChildRecursive("Background", Editorbility.Editable)]
	private Transform m_playersContainer;

	[SerializeField]
	private List<Vector3> m_playerRotations = new List<Vector3>();

	private IPlayerManager m_IPlayerManager;

	[SerializeField]
	private Camera m_chefCamera;

	[SerializeField]
	private Material m_renderMat;

	[SerializeField]
	private T17Image m_target;

	[SerializeField]
	private T17Image m_shadowTarget;

	private OrthoCameraResizer m_cameraResizer;

	[SerializeField]
	public string m_uiChefLayer;

	private RenderTexture m_rendTex;

	public bool m_canKickUsers;

	public bool m_isActive = true;

	private bool m_bShouldSetFocus = true;

	private SelectionCache m_selectionCache;

	private int m_rendTexID = -1;

	public bool AllowSettingFocus
	{
		get
		{
			return m_bShouldSetFocus;
		}
		set
		{
			m_bShouldSetFocus = value;
		}
	}

	public List<UIPlayerMenuBehaviour> UIPlayers
	{
		get
		{
			return m_players;
		}
	}

	protected override void Start()
	{
		base.Start();
		m_IPlayerManager = GameUtils.RequireManagerInterface<IPlayerManager>();
		m_selectionCache.Clear();
		ReCreateUIPlayers();
		if (m_bShouldSetFocus && m_isActive && base.CachedEventSystem != null && m_players.Count > 0 && m_players[0].CanBeInteractedWith())
		{
			base.CachedEventSystem.SetSelectedGameObject(m_players[0].MenuButton.gameObject);
		}
		else
		{
			PollForEventSystem();
		}
		m_chefCamera.cullingMask = LayerMask.GetMask(m_uiChefLayer);
		m_cameraResizer = m_chefCamera.gameObject.RequireComponent<OrthoCameraResizer>();
		m_target.material = m_renderMat;
		m_shadowTarget.material = m_renderMat;
		CreateRenderTexture();
		ClientUserSystem.usersChanged = (GenericVoid)Delegate.Combine(ClientUserSystem.usersChanged, new GenericVoid(OnUsersChanged));
	}

	public void ReCreateUIPlayers()
	{
		CacheSelection();
		UIPlayerMenuBehaviour[] componentsInChildren = m_playersContainer.GetComponentsInChildren<UIPlayerMenuBehaviour>();
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			UnityEngine.Object.Destroy(componentsInChildren[i].gameObject);
		}
		m_players.Clear();
		bool canKick = m_canKickUsers;
		if (ConnectionStatus.IsInSession() && !ConnectionStatus.IsHost())
		{
			canKick = false;
		}
		int num = ClientUserSystem.m_Users.Count;
		if (IsMatchmaking())
		{
			num = 4;
		}
		Selectable selectable = null;
		for (int j = 0; j < num; j++)
		{
			UIPlayerMenuBehaviour uIPlayerMenuBehaviour = UnityEngine.Object.Instantiate(m_uiPlayerPrefab, m_playersContainer);
			uIPlayerMenuBehaviour.m_rootMenu = this;
			uIPlayerMenuBehaviour.PlayerNumber = (j + 1).ToString();
			uIPlayerMenuBehaviour.UserInfo = ((j >= ClientUserSystem.m_Users.Count) ? null : ClientUserSystem.m_Users._items[j]);
			uIPlayerMenuBehaviour.m_canKick = canKick;
			if (j < m_playerRotations.Count)
			{
				uIPlayerMenuBehaviour.transform.localRotation = Quaternion.Euler(m_playerRotations[j]);
			}
			if (selectable != null && uIPlayerMenuBehaviour.UserInfo != null && uIPlayerMenuBehaviour.CanBeInteractedWith())
			{
				ConnectHorizontal(selectable, uIPlayerMenuBehaviour.MenuButton);
			}
			if (!uIPlayerMenuBehaviour.CanBeInteractedWith())
			{
				ColorBlock colors = uIPlayerMenuBehaviour.MenuButton.colors;
				colors.highlightedColor = colors.normalColor;
				uIPlayerMenuBehaviour.MenuButton.colors = colors;
				Navigation navigation = new Navigation
				{
					mode = Navigation.Mode.None
				};
				uIPlayerMenuBehaviour.MenuButton.navigation = navigation;
			}
			m_players.Add(uIPlayerMenuBehaviour);
			if (uIPlayerMenuBehaviour.UserInfo != null)
			{
				selectable = uIPlayerMenuBehaviour.MenuButton;
			}
		}
		m_BorderSelectables.selectOnUp = m_players[0].MenuButton;
		RestoreSelectionFromCache();
	}

	protected void ConnectHorizontal(Selectable _left, Selectable _right)
	{
		Navigation navigation = _left.navigation;
		Navigation navigation2 = _right.navigation;
		navigation.mode = Navigation.Mode.Explicit;
		navigation2.mode = Navigation.Mode.Explicit;
		navigation.selectOnRight = _right;
		navigation2.selectOnLeft = _left;
		_left.navigation = navigation;
		_right.navigation = navigation2;
	}

	protected void CreateRenderTexture()
	{
		m_chefCamera.targetTexture = null;
		if (m_rendTex != null)
		{
			RenderTargetManager.ReleaseRenderTarget(ref m_rendTexID);
			m_rendTex = null;
		}
		Vector2 targetDimensions = m_cameraResizer.TargetDimensions;
		m_rendTex = RenderTargetManager.RequestRenderTarget(Mathf.RoundToInt(targetDimensions.x), Mathf.RoundToInt(targetDimensions.y), 16, RenderTextureFormat.ARGB32, ref m_rendTexID);
		m_target.material.SetTexture("_MainTex", m_rendTex);
		m_target.SetMaterialDirty();
		m_shadowTarget.material.SetTexture("_MainTex", m_rendTex);
		m_shadowTarget.SetMaterialDirty();
		m_rendTex.filterMode = FilterMode.Bilinear;
		m_chefCamera.targetTexture = m_rendTex;
	}

	protected override void Update()
	{
		base.Update();
		Vector2 targetDimensions = m_cameraResizer.TargetDimensions;
		if (m_rendTex == null || (float)m_rendTex.width != targetDimensions.x || (float)m_rendTex.height != targetDimensions.y)
		{
			CreateRenderTexture();
		}
		PollForEventSystem();
	}

	protected void PollForEventSystem()
	{
		if (!m_isActive)
		{
			m_CachedEventSystem = null;
			return;
		}
		bool flag = base.CachedEventSystem != null;
		GamepadUser user = m_IPlayerManager.GetUser(EngagementSlot.One);
		if (!flag)
		{
			m_CachedEventSystem = ((!(user != null)) ? null : T17EventSystemsManager.Instance.GetEventSystemForGamepadUser(user));
		}
		if (m_bShouldSetFocus && !flag)
		{
			FocusOnFirstPlayer();
		}
	}

	public UIPlayerMenuBehaviour GetUIPlayerForUser(string _id)
	{
		for (int i = 0; i < m_players.Count; i++)
		{
			UIPlayerMenuBehaviour uIPlayerMenuBehaviour = m_players[i];
			if (uIPlayerMenuBehaviour != null && uIPlayerMenuBehaviour.UserInfo != null && uIPlayerMenuBehaviour.UserInfo.DisplayName == _id)
			{
				return m_players[i];
			}
		}
		return null;
	}

	public UIPlayerMenuBehaviour GetUIPlayerForUser(User _user)
	{
		for (int i = 0; i < m_players.Count; i++)
		{
			if (m_players[i] != null && m_players[i].UserInfo == _user)
			{
				return m_players[i];
			}
		}
		return null;
	}

	protected void OnUsersChanged()
	{
		CacheSelection();
		Selectable selectable = null;
		for (int i = 0; i < m_players.Count; i++)
		{
			UIPlayerMenuBehaviour uIPlayerMenuBehaviour = m_players[i];
			uIPlayerMenuBehaviour.UserInfo = ((i >= ClientUserSystem.m_Users.Count) ? null : ClientUserSystem.m_Users._items[i]);
			if (selectable != null && uIPlayerMenuBehaviour.UserInfo != null && uIPlayerMenuBehaviour.CanBeInteractedWith())
			{
				ConnectHorizontal(selectable, uIPlayerMenuBehaviour.MenuButton);
			}
			if (!uIPlayerMenuBehaviour.CanBeInteractedWith())
			{
				ColorBlock colors = uIPlayerMenuBehaviour.MenuButton.colors;
				colors.highlightedColor = colors.normalColor;
				uIPlayerMenuBehaviour.MenuButton.colors = colors;
				Navigation navigation = new Navigation
				{
					mode = Navigation.Mode.None
				};
				uIPlayerMenuBehaviour.MenuButton.navigation = navigation;
			}
			if (uIPlayerMenuBehaviour.UserInfo != null)
			{
				selectable = uIPlayerMenuBehaviour.MenuButton;
			}
		}
		m_BorderSelectables.selectOnUp = m_players[0].MenuButton;
		RestoreSelectionFromCache();
	}

	public bool IsMatchmaking()
	{
		LobbySetupInfo instance = LobbySetupInfo.Instance;
		if (SceneManager.GetActiveScene().name == "Lobbies")
		{
			if (instance != null)
			{
				if (instance.m_connectionMode == OnlineMultiplayerConnectionMode.eInternet && instance.m_visiblity == OnlineMultiplayerSessionVisibility.eMatchmaking)
				{
					return true;
				}
				if (instance.m_visiblity == OnlineMultiplayerSessionVisibility.eClosed || instance.m_visiblity == OnlineMultiplayerSessionVisibility.ePrivate)
				{
					return false;
				}
			}
			else if (ConnectionModeSwitcher.GetRequestedConnectionState() == NetConnectionState.Matchmake)
			{
				return true;
			}
		}
		return false;
	}

	private void CacheSelection()
	{
		m_selectionCache.Clear();
		if (!(base.CachedEventSystem != null))
		{
			return;
		}
		GameObject gameObject = base.CachedEventSystem.GetPendingSelectedGameObject();
		if (gameObject == null)
		{
			gameObject = base.CachedEventSystem.currentSelectedGameObject;
		}
		if (gameObject != null && gameObject.IsInHierarchyOf(base.gameObject))
		{
			UIPlayerMenuBehaviour uiPlayer = gameObject.RequestComponentRecursive<UIPlayerMenuBehaviour>();
			if (uiPlayer == null)
			{
				uiPlayer = gameObject.RequestComponentUpwardsRecursive<UIPlayerMenuBehaviour>();
			}
			if (uiPlayer != null)
			{
				m_selectionCache.m_selectedUserName = uiPlayer.LastSetDisplayName;
				m_selectionCache.m_selectedIdx = m_players.FindIndex((UIPlayerMenuBehaviour x) => x == uiPlayer);
				m_selectionCache.m_selectedOption = uiPlayer.GetSelectedMenuOption();
			}
		}
		m_selectionCache.m_hasCached = true;
	}

	private void RestoreSelectionFromCache()
	{
		if (!m_selectionCache.m_hasCached)
		{
			return;
		}
		if (m_selectionCache.m_selectedUserName != null && base.CachedEventSystem != null)
		{
			GameObject pendingSelectedGameObject = base.CachedEventSystem.GetPendingSelectedGameObject();
			if (pendingSelectedGameObject == null || pendingSelectedGameObject.IsInHierarchyOf(base.gameObject))
			{
				UIPlayerMenuBehaviour uIPlayerForUser = GetUIPlayerForUser(m_selectionCache.m_selectedUserName);
				if (uIPlayerForUser != null)
				{
					UIPlayerMenuBehaviour.UIPlayerMenuOptions? selectedOption = m_selectionCache.m_selectedOption;
					if (selectedOption.HasValue)
					{
						uIPlayerForUser.ShowMenu(m_selectionCache.m_selectedOption);
					}
					else
					{
						base.CachedEventSystem.SetSelectedGameObject(uIPlayerForUser.MenuButton.gameObject);
					}
				}
				else if (m_selectionCache.m_selectedIdx > 0)
				{
					int num = Mathf.Min(m_selectionCache.m_selectedIdx, m_players.Count - 1);
					for (int num2 = num; num2 >= 0; num2--)
					{
						Selectable menuButton = m_players[num2].MenuButton;
						if (menuButton.isActiveAndEnabled && menuButton.IsInteractable())
						{
							base.CachedEventSystem.SetSelectedGameObject(menuButton.gameObject);
							break;
						}
					}
				}
				else
				{
					FocusOnFirstPlayer(true);
				}
			}
		}
		m_selectionCache.Clear();
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		if (m_rendTex != null)
		{
			m_chefCamera.targetTexture = null;
			RenderTargetManager.ReleaseRenderTarget(ref m_rendTexID);
			m_rendTex = null;
		}
		ClientUserSystem.usersChanged = (GenericVoid)Delegate.Remove(ClientUserSystem.usersChanged, new GenericVoid(OnUsersChanged));
	}

	public bool CanFocusOnFirstPlayer(bool _force = false)
	{
		if (base.CachedEventSystem != null && m_players.Count > 0 && m_players[0].CanBeInteractedWith() && (_force || base.CachedEventSystem.currentSelectedGameObject == null || !base.CachedEventSystem.currentSelectedGameObject.activeInHierarchy) && (_force || base.CachedEventSystem.GetLastRequestedSelectedGameobject() == null))
		{
			return true;
		}
		return false;
	}

	public void FocusOnFirstPlayer(bool _force = false)
	{
		if (CanFocusOnFirstPlayer(_force))
		{
			base.CachedEventSystem.SetSelectedGameObject(m_players[0].MenuButton.gameObject);
		}
	}

	public void CloseAllPlayerMenus()
	{
		for (int i = 0; i < m_players.Count; i++)
		{
			if (m_players[i] != null)
			{
				m_players[i].HideMenu(true);
			}
		}
	}
}
