using System;
using System.Collections;
using System.Collections.Generic;
using Team17.Online;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(T17Button))]
public class UIPlayerMenuBehaviour : BaseMenuBehaviour
{
	[Serializable]
	public struct MenuOption
	{
		[SerializeField]
		public T17Button m_button;

		[SerializeField]
		public UIPlayerMenuOptions m_type;

		[SerializeField]
		public bool m_disableIfLocal;

		[Mask(typeof(PlatformUtils.Platforms))]
		public int m_platformsActiveOn;
	}

	[Serializable]
	public struct NameStrings
	{
		[SerializeField]
		public T17Text m_displayName;

		[SerializeField]
		public T17Text m_searching;

		[SerializeField]
		public T17Text m_empty;
	}

	[Serializable]
	public enum UIPlayerMenuOptions
	{
		Name = 0,
		Profile = 1,
		Mute = 2,
		Unmute = 3,
		Kick = 4
	}

	protected enum SelectableDirection
	{
		Up = 0,
		Down = 1
	}

	[SerializeField]
	private List<MenuOption> m_menuOptions;

	[SerializeField]
	private NameStrings m_nameOptions;

	[SerializeField]
	[AssignChildRecursive("TalkingIcon", Editorbility.Editable)]
	private T17Image m_speakerImage;

	[SerializeField]
	[AssignChildRecursive("MuteIcon", Editorbility.Editable)]
	private T17Image m_muteImage;

	[SerializeField]
	[AssignChildRecursive("LoadingIconParent", Editorbility.Editable)]
	private GameObject m_loadingIcon;

	[SerializeField]
	[AssignChildRecursive("Background", Editorbility.Editable)]
	private T17Image m_background;

	[SerializeField]
	[AssignChildRecursive("Menu", Editorbility.Editable)]
	private GameObject m_menu;

	public bool m_canKick;

	[SerializeField]
	[AssignChild("NameBacker", Editorbility.Editable)]
	private T17Button m_nameButton;

	[HideInInspector]
	public UIPlayerRootMenu m_rootMenu;

	[SerializeField]
	private T17Text m_playerNumber;

	[SerializeField]
	private Transform m_dialogAnchor;

	[SerializeField]
	private Transform m_emoteWheelAnchor;

	[SerializeField]
	private ChefColourData m_teamOne;

	[SerializeField]
	private ChefColourData m_teamTwo;

	[SerializeField]
	private ChefColourData m_teamNone;

	[SerializeField]
	private ChefColourData m_noChef;

	[SerializeField]
	private Color m_AmbientColor;

	private FrontendChef m_chef;

	private User m_User;

	private string m_lastSetDisplayName;

	[SerializeField]
	private VoiceChatUserUI m_voiceChatUI;

	private IPlayerManager m_IPlayerManager;

	private Coroutine m_hideMenu;

	public Selectable MenuButton
	{
		get
		{
			return m_nameButton;
		}
	}

	public Transform DialogAnchor
	{
		get
		{
			return m_dialogAnchor;
		}
	}

	public Transform EmoteWheelAnchor
	{
		get
		{
			return m_emoteWheelAnchor;
		}
	}

	public string LastSetDisplayName
	{
		get
		{
			return m_lastSetDisplayName;
		}
	}

	public User UserInfo
	{
		get
		{
			return m_User;
		}
		set
		{
			m_User = value;
			if (m_User != null)
			{
				m_nameOptions.m_displayName.text = m_User.DisplayName;
				m_nameOptions.m_displayName.gameObject.SetActive(true);
				m_nameOptions.m_searching.gameObject.SetActive(false);
				m_loadingIcon.SetActive(false);
				m_nameOptions.m_empty.gameObject.SetActive(false);
				m_playerNumber.enabled = true;
				int num = ClientUserSystem.m_Users.FindIndex((User user) => user == m_User);
				m_voiceChatUI.PlayerSlot = ((num == -1) ? EngagementSlot.Count : ((EngagementSlot)num));
				m_lastSetDisplayName = m_User.DisplayName;
			}
			else
			{
				m_nameOptions.m_displayName.gameObject.SetActive(false);
				bool flag = m_rootMenu.IsMatchmaking();
				m_nameOptions.m_searching.gameObject.SetActive(flag);
				m_loadingIcon.SetActive(flag);
				m_nameOptions.m_empty.gameObject.SetActive(!flag);
				HideMenu(true);
				m_playerNumber.enabled = false;
				m_lastSetDisplayName = null;
			}
			m_nameButton.interactable = CanBeInteractedWith();
			UpdateColour();
		}
	}

	public string PlayerNumber
	{
		get
		{
			return m_playerNumber.text;
		}
		set
		{
			m_playerNumber.text = value;
		}
	}

	protected override void Awake()
	{
		base.Awake();
		HideMenu(true);
	}

	protected override void Start()
	{
		base.Start();
		if (m_nameButton == null)
		{
			m_nameButton = GetComponent<T17Button>();
		}
		m_IPlayerManager = GameUtils.RequireManagerInterface<IPlayerManager>();
		T17Button nameButton = m_nameButton;
		nameButton.OnButtonSelect = (T17Button.T17ButtonDelegate)Delegate.Combine(nameButton.OnButtonSelect, new T17Button.T17ButtonDelegate(OnSelected));
		T17Button nameButton2 = m_nameButton;
		nameButton2.OnButtonDeselect = (T17Button.T17ButtonDelegate)Delegate.Combine(nameButton2.OnButtonDeselect, new T17Button.T17ButtonDelegate(OnDeselected));
		m_nameButton.onClick.AddListener(delegate
		{
			OnClick(UIPlayerMenuOptions.Name);
		});
		for (int num = 0; num < m_menuOptions.Count; num++)
		{
			MenuOption option = m_menuOptions[num];
			T17Button button = option.m_button;
			button.OnButtonSelect = (T17Button.T17ButtonDelegate)Delegate.Combine(button.OnButtonSelect, new T17Button.T17ButtonDelegate(OnSelected));
			T17Button button2 = option.m_button;
			button2.OnButtonDeselect = (T17Button.T17ButtonDelegate)Delegate.Combine(button2.OnButtonDeselect, new T17Button.T17ButtonDelegate(OnDeselected));
			option.m_button.onClick.AddListener(delegate
			{
				OnClick(option.m_type);
			});
		}
		SetupChefModel(true);
		base.gameObject.layer = LayerMask.NameToLayer(m_rootMenu.m_uiChefLayer);
		m_background.transform.SetParent(m_rootMenu.m_backgroundsContainer);
		InGameCustomisation instance = InGameCustomisation.Instance;
		if (instance != null)
		{
			m_nameButton.interactable = CanBeInteractedWith();
			instance.RegisterOnActiveToggle(OnCustomisationToggle);
		}
	}

	protected override void Update()
	{
		base.Update();
		if ((m_nameButton.navigation.selectOnUp != null && !m_nameButton.navigation.selectOnUp.IsInteractable()) || (m_nameButton.navigation.selectOnDown != null && !m_nameButton.navigation.selectOnDown.IsInteractable()))
		{
			UpdateNavigation();
		}
		if (m_hideMenu == null && IsSelected())
		{
			m_hideMenu = StartCoroutine(HideMenuRoutine());
		}
	}

	protected void SetupChefModel(bool _force = false)
	{
		if (m_User == null)
		{
			if (m_chef != null && m_chef.ChefModel != null)
			{
				m_chef.ChefModel.SetActive(false);
			}
			return;
		}
		if (m_chef != null && m_chef.ChefModel != null)
		{
			m_chef.ChefModel.SetActive(true);
		}
		base.gameObject.layer = LayerMask.NameToLayer(m_rootMenu.m_uiChefLayer);
		m_chef = base.gameObject.RequireComponent<FrontendChef>();
		GameObject chefModel = m_chef.ChefModel;
		m_chef.SetChefData(m_User.SelectedChefData, _force);
		m_chef.SetChefHat(HatMeshVisibility.VisState.Fancy);
		m_chef.SetShaderMode(FrontendChef.ShaderMode.eUI);
		m_chef.SetUIChefAmbientLighting(m_AmbientColor);
		if (m_chef.ChefModel != chefModel)
		{
			int num = ClientUserSystem.m_Users.FindIndex((User x) => x == m_User);
			if (num != -1)
			{
				m_chef.SetAnimationSet((FrontendChef.AnimationSet)num);
			}
		}
	}

	private ChefColourData GetTeamColour()
	{
		ChefColourData result = m_noChef;
		MetaGameProgress metaGameProgress = GameUtils.GetMetaGameProgress();
		if (metaGameProgress == null)
		{
			return result;
		}
		AvatarDirectoryData avatarDirectory = metaGameProgress.AvatarDirectory;
		if (avatarDirectory == null)
		{
			return result;
		}
		if (m_User == null)
		{
			return result;
		}
		bool flag = true;
		if (ClientGameSetup.Mode == GameMode.Versus)
		{
			flag = false;
		}
		else
		{
			GameSession gameSession = GameUtils.GetGameSession();
			if (gameSession != null && gameSession.TypeSettings.Type == GameSession.GameType.Competitive)
			{
				flag = false;
			}
		}
		switch (m_User.Team)
		{
		case TeamID.None:
			if (flag)
			{
				int num = ClientUserSystem.m_Users.FindIndex((User x) => x == m_User);
				if (num != -1)
				{
					result = metaGameProgress.AvatarDirectory.Colours[num];
				}
			}
			else
			{
				result = m_teamNone;
			}
			break;
		case TeamID.One:
			result = m_teamOne;
			break;
		case TeamID.Two:
			result = m_teamTwo;
			break;
		}
		return result;
	}

	public void UpdateBackground()
	{
		ChefColourData teamColour = GetTeamColour();
		m_background.sprite = teamColour.Background;
		m_speakerImage.color = teamColour.DarkUIColour;
		m_muteImage.color = teamColour.DarkUIColour;
		m_playerNumber.color = teamColour.DarkUIColour;
	}

	public void UpdateColour()
	{
		if (m_User != null)
		{
			UpdateBackground();
			SetupChefModel();
			return;
		}
		if (m_chef != null && m_chef.ChefModel != null)
		{
			m_chef.ChefModel.SetActive(false);
		}
		UpdateBackground();
	}

	public bool HasMenuOptions()
	{
		if (m_User != null)
		{
			for (int i = 0; i < m_menuOptions.Count; i++)
			{
				MenuOption option = m_menuOptions[i];
				if (IsOptionActiveOnCurrentPlatform(option) && (!m_User.IsLocal || !option.m_disableIfLocal))
				{
					return true;
				}
			}
		}
		return false;
	}

	private bool IsOptionActiveOnCurrentPlatform(MenuOption _option)
	{
		return PlatformUtils.HasPlatformFlag(_option.m_platformsActiveOn);
	}

	protected void OnClick(UIPlayerMenuOptions _option)
	{
		if (!HasMenuOptions())
		{
			return;
		}
		switch (_option)
		{
		case UIPlayerMenuOptions.Name:
			ToggleMenu();
			break;
		case UIPlayerMenuOptions.Kick:
			KickUser();
			break;
		case UIPlayerMenuOptions.Mute:
			SetMuted(true);
			break;
		case UIPlayerMenuOptions.Unmute:
			SetMuted(false);
			break;
		case UIPlayerMenuOptions.Profile:
			if (m_User.IsLocal)
			{
				m_IPlayerManager.ShowGamerCard(m_User.GamepadUser);
			}
			else
			{
				m_IPlayerManager.ShowGamerCard(m_User.PlatformID);
			}
			break;
		}
	}

	protected void SetMuted(bool toMuted)
	{
		m_User.SessionId.IsLocallyMuted = toMuted;
		UpdateMenuStructure();
		for (int i = 0; i < m_menuOptions.Count; i++)
		{
			if (m_menuOptions[i].m_type == UIPlayerMenuOptions.Mute && !toMuted)
			{
				m_rootMenu.CachedEventSystem.SetSelectedGameObject(m_menuOptions[i].m_button.gameObject);
			}
			else if (m_menuOptions[i].m_type == UIPlayerMenuOptions.Unmute && toMuted)
			{
				m_rootMenu.CachedEventSystem.SetSelectedGameObject(m_menuOptions[i].m_button.gameObject);
			}
		}
	}

	protected virtual void OnSelected(T17Button _button)
	{
		if ((_button == m_nameButton || _button.gameObject.IsInHierarchyOf(m_menu)) && m_hideMenu != null)
		{
			StopCoroutine(m_hideMenu);
		}
		UpdateNavigation();
	}

	protected virtual void OnDeselected(T17Button _button)
	{
		HideMenu();
		UpdateNavigation();
	}

	private IEnumerator HideMenuRoutine(bool _force = false)
	{
		if (!_force)
		{
			yield return new WaitForEndOfFrame();
			while (IsSelected() && (!Input.mousePresent || !Input.GetMouseButtonDown(0)))
			{
				yield return null;
			}
			if (T17DialogBoxManager.HasAnyOpenDialogs())
			{
				m_hideMenu = null;
				yield break;
			}
		}
		m_menu.SetActive(false);
		UpdateNavigation();
	}

	protected void UpdateNavigation()
	{
		UpdateChildNavigation();
		Navigation navigation = m_nameButton.navigation;
		Selectable firstSelectableChild = GetFirstSelectableChild();
		navigation.selectOnUp = firstSelectableChild;
		if (navigation.selectOnUp == null)
		{
			navigation.selectOnUp = m_nameButton.FindSelectable(m_nameButton.transform.up);
		}
		if (navigation.selectOnDown == null)
		{
			navigation.selectOnDown = m_nameButton.FindSelectable(-m_nameButton.transform.up);
		}
		m_nameButton.navigation = navigation;
	}

	protected Selectable GetFirstSelectableChild()
	{
		Selectable[] array = m_menu.RequestComponentsInImmediateChildren<Selectable>();
		for (int num = array.Length - 1; num >= 0; num--)
		{
			if (array[num].isActiveAndEnabled)
			{
				return array[num];
			}
		}
		return null;
	}

	protected Selectable GetAdjacentSelectable(Selectable selectable, SelectableDirection direction)
	{
		Selectable[] array = m_menu.RequestComponentsInImmediateChildren<Selectable>();
		int num = array.FindIndex_Predicate((Selectable x) => x == selectable);
		if (num == -1)
		{
			return null;
		}
		for (int num2 = ((direction != SelectableDirection.Up) ? 1 : (-1)); num + num2 >= 0 && num + num2 < array.Length; num2 += ((direction != SelectableDirection.Up) ? 1 : (-1)))
		{
			if (array[num + num2].isActiveAndEnabled)
			{
				return array[num + num2];
			}
		}
		if (direction == SelectableDirection.Up)
		{
			return selectable.FindSelectable(m_nameButton.transform.up);
		}
		return selectable.FindSelectable(-m_nameButton.transform.up);
	}

	protected void UpdateChildNavigation()
	{
		for (int i = 0; i < m_menuOptions.Count; i++)
		{
			if (m_menuOptions[i].m_button.isActiveAndEnabled)
			{
				Navigation navigation = m_menuOptions[i].m_button.navigation;
				navigation.mode = Navigation.Mode.Explicit;
				navigation.selectOnUp = GetAdjacentSelectable(m_menuOptions[i].m_button, SelectableDirection.Up);
				Selectable selectable = GetAdjacentSelectable(m_menuOptions[i].m_button, SelectableDirection.Down);
				if (selectable == null)
				{
					selectable = m_nameButton;
				}
				navigation.selectOnDown = selectable;
				navigation.selectOnLeft = m_nameButton.navigation.selectOnLeft;
				navigation.selectOnRight = m_nameButton.navigation.selectOnRight;
				m_menuOptions[i].m_button.navigation = navigation;
			}
		}
	}

	protected bool IsSelected()
	{
		if (m_rootMenu.CachedEventSystem == null)
		{
			return false;
		}
		GameObject currentSelectedGameObject = m_rootMenu.CachedEventSystem.currentSelectedGameObject;
		return currentSelectedGameObject == this || currentSelectedGameObject == null;
	}

	public void ShowMenu(UIPlayerMenuOptions? _optionToSelect = null)
	{
		m_menu.SetActive(true);
		UpdateMenuStructure();
		if (!_optionToSelect.HasValue || !(m_rootMenu.CachedEventSystem != null))
		{
			return;
		}
		for (int i = 0; i < m_menuOptions.Count; i++)
		{
			MenuOption menuOption = m_menuOptions[i];
			if (menuOption.m_type == _optionToSelect.GetValueOrDefault() && _optionToSelect.HasValue)
			{
				m_rootMenu.CachedEventSystem.SetSelectedGameObject(menuOption.m_button.gameObject);
				break;
			}
		}
	}

	public UIPlayerMenuOptions? GetSelectedMenuOption()
	{
		if (m_rootMenu.CachedEventSystem == null)
		{
			return null;
		}
		GameObject gameObject = m_rootMenu.CachedEventSystem.GetPendingSelectedGameObject();
		if (gameObject == null)
		{
			gameObject = m_rootMenu.CachedEventSystem.currentSelectedGameObject;
		}
		if (gameObject == null)
		{
			return null;
		}
		for (int i = 0; i < m_menuOptions.Count; i++)
		{
			MenuOption menuOption = m_menuOptions[i];
			if (menuOption.m_button.gameObject == gameObject)
			{
				return menuOption.m_type;
			}
		}
		return null;
	}

	protected void KickUser()
	{
	}

	protected void UpdateMenuStructure()
	{
		for (int i = 0; i < m_menuOptions.Count; i++)
		{
			UIPlayerMenuOptions type = m_menuOptions[i].m_type;
			bool flag = IsOptionActiveOnCurrentPlatform(m_menuOptions[i]);
			if (flag)
			{
				if (m_User == null)
				{
					flag = false;
				}
				else if (m_User.IsLocal && m_menuOptions[i].m_disableIfLocal)
				{
					flag = false;
				}
				else
				{
					switch (type)
					{
					case UIPlayerMenuOptions.Mute:
					case UIPlayerMenuOptions.Unmute:
						if (m_User.SessionId == null || (type == UIPlayerMenuOptions.Mute && m_User.SessionId.IsLocallyMuted) || (type == UIPlayerMenuOptions.Unmute && !m_User.SessionId.IsLocallyMuted))
						{
							flag = false;
						}
						break;
					case UIPlayerMenuOptions.Kick:
						if (!m_canKick && !m_User.IsLocal)
						{
							flag = false;
						}
						break;
					}
				}
			}
			m_menuOptions[i].m_button.gameObject.SetActive(flag);
		}
		UpdateNavigation();
	}

	public void HideMenu(bool _force = false)
	{
		m_hideMenu = StartCoroutine(HideMenuRoutine(_force));
	}

	public void ToggleMenu()
	{
		if (m_menu.activeSelf)
		{
			HideMenu();
		}
		else
		{
			ShowMenu();
		}
	}

	protected void OnCustomisationToggle(bool _active)
	{
		if (m_nameButton != null)
		{
			m_nameButton.interactable = CanBeInteractedWith();
		}
	}

	public bool CanBeInteractedWith()
	{
		if (m_User == null)
		{
			return false;
		}
		InGameCustomisation instance = InGameCustomisation.Instance;
		if (instance != null && instance.isActiveAndEnabled)
		{
			return false;
		}
		if (!HasMenuOptions())
		{
			return false;
		}
		return true;
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		UnityEngine.Object.Destroy(m_background.gameObject);
		InGameCustomisation instance = InGameCustomisation.Instance;
		if (instance != null)
		{
			instance.UnregisterOnActiveToggle(OnCustomisationToggle);
		}
	}
}
