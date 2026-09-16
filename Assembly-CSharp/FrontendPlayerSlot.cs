using System;
using System.Collections.Generic;
using Team17.Online;
using UnityEngine;

public class FrontendPlayerSlot : MonoBehaviour
{
	public EngagementSlot m_EngagementSlot;

	public Color m_DisabledSlotColour = Color.gray;

	public T17Image m_SlotDisabledImage;

	public GameObject m_SlotDisabledHosting;

	public T17Image m_AddPlayerImage;

	public T17Image m_selectedAddPlayerImage;

	public T17Button m_SlotButton;

	public T17Image m_ControllerTypeImage;

	public T17Image m_GamerpicImage;

	public T17Image m_frameImage;

	public T17Image m_selectedFrameImage;

	public Sprite m_remotePlayerIcon;

	public T17Text m_userNameText;

	[SerializeField]
	private string m_HostTooltip;

	[SerializeField]
	private string m_ClientTooltip;

	[SerializeField]
	private string m_LocalTooltip;

	[SerializeField]
	private string m_AddPlayerTooltip;

	private bool m_isSelected;

	private bool m_isInitialised;

	private Texture2D m_lastAvatarImage;

	private GamepadUser m_CurrentgamepadUser;

	private PadSide m_CurrentpadSide = PadSide.Both;

	[AssignResource("Frontend_ControllerTypeSprites", Editorbility.NonEditable)]
	public ControllerTypeSprites m_ControllerSprites;

	[AssignResource("MainAvatarDirectory", Editorbility.NonEditable)]
	public AvatarDirectoryData m_AvatarDirectory;

	private T17Image m_SlotButtonImage;

	private IPlayerManager m_IPlayerManager;

	private Color m_GreyedOutColour = new Color(0.5f, 0.5f, 0.5f);

	private int m_userIndex;

	private Sprite m_gamerPicDefaultSprite;

	public GamepadUser CurrentGamepadUser
	{
		get
		{
			return m_CurrentgamepadUser;
		}
	}

	public PadSide CurrentPadSide
	{
		get
		{
			return m_CurrentpadSide;
		}
	}

	private void Awake()
	{
		if (m_SlotButton == null)
		{
			m_SlotButton = GetComponent<T17Button>();
		}
		if (m_ControllerTypeImage == null)
		{
		}
		if (m_SlotButtonImage == null && m_SlotButton != null)
		{
			m_SlotButtonImage = m_SlotButton.GetComponent<T17Image>();
		}
		m_IPlayerManager = GameUtils.RequireManagerInterface<IPlayerManager>();
		ClientUserSystem.usersChanged = (GenericVoid)Delegate.Remove(ClientUserSystem.usersChanged, new GenericVoid(OnUsersChanged));
		ClientUserSystem.usersChanged = (GenericVoid)Delegate.Combine(ClientUserSystem.usersChanged, new GenericVoid(OnUsersChanged));
		m_IPlayerManager.EngagementChangeCallback += OnEngagementChanged;
		if (null != m_GamerpicImage && null != m_GamerpicImage.sprite)
		{
			m_gamerPicDefaultSprite = m_GamerpicImage.sprite;
		}
		m_isInitialised = true;
		if (PlayerInputLookup.IsAwake())
		{
			RefreshCosmetics();
		}
	}

	private void OnDestroy()
	{
		ClientUserSystem.usersChanged = (GenericVoid)Delegate.Remove(ClientUserSystem.usersChanged, new GenericVoid(OnUsersChanged));
		m_IPlayerManager.EngagementChangeCallback -= OnEngagementChanged;
	}

	private void OnEngagementChanged(EngagementSlot _param1, GamepadUser _param2, GamepadUser _param3)
	{
		if (PlayerInputLookup.IsAwake())
		{
			RefreshCosmetics();
		}
	}

	public void OnUsersChanged()
	{
		RefreshCosmetics();
	}

	public void OnConnectionModeUpdated()
	{
		RefreshCosmetics();
	}

	public void SelectSlot()
	{
		m_isSelected = true;
		RefreshCosmetics();
	}

	public void DeselectSlot()
	{
		m_isSelected = false;
		RefreshCosmetics();
	}

	private void RefreshCosmetics()
	{
		if (m_isInitialised)
		{
			m_userIndex = (int)m_EngagementSlot;
			bool flag = !ConnectionStatus.IsInSession() || ConnectionStatus.IsHost();
			FastList<User> users = ClientUserSystem.m_Users;
			if (m_userIndex < users.Count)
			{
				SetSlotEnabledForUser(users._items[m_userIndex]);
			}
			else if (m_userIndex == users.Count && flag)
			{
				SetSlotEnabledForAdd();
			}
			else
			{
				SetSlotDisabled();
			}
		}
	}

	private void SetSlotDisabled()
	{
		m_CurrentpadSide = PadSide.Both;
		m_CurrentgamepadUser = null;
		if (m_AddPlayerImage != null)
		{
			m_AddPlayerImage.gameObject.SetActive(false);
		}
		if (m_SlotButton != null)
		{
			m_SlotButton.interactable = false;
		}
		if (ConnectionStatus.IsHost())
		{
			if (m_SlotDisabledHosting != null)
			{
				m_SlotDisabledHosting.SetActive(true);
			}
			if (m_SlotDisabledImage != null)
			{
				m_SlotDisabledImage.gameObject.SetActive(false);
			}
		}
		else
		{
			if (m_SlotDisabledImage != null)
			{
				m_SlotDisabledImage.gameObject.SetActive(true);
			}
			if (m_SlotDisabledHosting != null)
			{
				m_SlotDisabledHosting.SetActive(false);
			}
		}
		SetUIColourForUser(null);
		SetControllerIconForUser(null);
		SetGamerpicForUser(null);
		SetSlotSelectedHighlight(false, false);
		SetNameTextForUser(null);
	}

	private void SetSlotEnabledForAdd()
	{
		m_CurrentpadSide = PadSide.Both;
		m_CurrentgamepadUser = null;
		if (m_AddPlayerImage != null)
		{
			m_AddPlayerImage.gameObject.SetActive(true);
		}
		if (m_SlotDisabledImage != null)
		{
			m_SlotDisabledImage.gameObject.SetActive(false);
		}
		if (m_SlotDisabledHosting != null)
		{
			m_SlotDisabledHosting.SetActive(false);
		}
		if (m_SlotButton != null)
		{
			m_SlotButton.interactable = true;
			m_SlotButton.image = m_AddPlayerImage;
			m_SlotButton.m_bShowTooltip = true;
			m_SlotButton.m_bLocalizeTooltip = true;
			m_SlotButton.m_TooltipTag = m_AddPlayerTooltip;
		}
		SetUIColourForUser(null);
		SetControllerIconForUser(null);
		SetGamerpicForUser(null);
		SetSlotSelectedHighlight(true, false);
	}

	private void SetSlotEnabledForUser(User user)
	{
		if (m_SlotDisabledImage != null)
		{
			m_SlotDisabledImage.gameObject.SetActive(false);
		}
		if (m_SlotDisabledHosting != null)
		{
			m_SlotDisabledHosting.SetActive(false);
		}
		if (m_AddPlayerImage != null)
		{
			m_AddPlayerImage.gameObject.SetActive(false);
		}
		if (m_SlotButton != null)
		{
			m_SlotButton.interactable = true;
			m_SlotButton.image = m_frameImage;
			m_SlotButton.m_bShowTooltip = false;
		}
		if (user == null)
		{
			return;
		}
		if (m_SlotButton != null)
		{
			m_SlotButton.m_bShowTooltip = true;
			m_SlotButton.m_bLocalizeTooltip = true;
			if (user.IsLocal)
			{
				if (user.Engagement == EngagementSlot.One && user.Split != User.SplitStatus.SplitPadGuest)
				{
					m_SlotButton.m_TooltipTag = m_HostTooltip;
				}
				else
				{
					m_SlotButton.m_TooltipTag = m_LocalTooltip;
				}
			}
			else if (user.Engagement == EngagementSlot.One)
			{
				m_SlotButton.m_TooltipTag = m_HostTooltip;
			}
			else
			{
				m_SlotButton.m_TooltipTag = m_ClientTooltip;
			}
		}
		if (m_IPlayerManager != null)
		{
			m_CurrentgamepadUser = m_IPlayerManager.GetUser(user.Engagement);
		}
		SetUIColourForUser(user);
		SetControllerIconForUser(user);
		SetGamerpicForUser(user);
		SetSlotSelectedHighlight(true, true);
		SetNameTextForUser(user);
	}

	private string Ellipsify(string _input)
	{
		if (_input.Length <= 16)
		{
			return _input;
		}
		return _input.Substring(0, 16) + "...";
	}

	private void SetUIColourForUser(User user)
	{
		Color color = m_DisabledSlotColour;
		Color color2 = m_GreyedOutColour;
		if (user != null && user.Colour != 7 && (int)user.Colour < m_AvatarDirectory.Colours.Length)
		{
			ChefColourData chefColourData = m_AvatarDirectory.Colours[user.Colour];
			color = chefColourData.PadUIColour;
			color2 = chefColourData.PadBarColour;
		}
		if (m_ControllerTypeImage != null)
		{
			m_ControllerTypeImage.color = color;
		}
		if (m_SlotButtonImage != null)
		{
			m_SlotButtonImage.color = color;
		}
		if (m_userNameText != null)
		{
			m_userNameText.color = color;
		}
		if (!(color2 == m_GreyedOutColour))
		{
		}
	}

	private void SetControllerIconForUser(User user)
	{
		if (m_ControllerTypeImage == null)
		{
			return;
		}
		if (user == null)
		{
			m_ControllerTypeImage.gameObject.SetActive(false);
		}
		else if (!user.IsLocal)
		{
			if (m_remotePlayerIcon != null)
			{
				m_ControllerTypeImage.gameObject.SetActive(true);
				m_ControllerTypeImage.sprite = m_remotePlayerIcon;
			}
		}
		else if (m_CurrentgamepadUser != null)
		{
			m_ControllerTypeImage.gameObject.SetActive(true);
			m_CurrentpadSide = user.PadSide;
			Sprite image = m_ControllerSprites.GetImage(m_CurrentpadSide, m_CurrentgamepadUser.ControlType);
			m_ControllerTypeImage.sprite = image;
		}
	}

	private void SetGamerpicForUser(User user, bool _force = false)
	{
		if (null == m_GamerpicImage)
		{
			return;
		}
		if (user != null)
		{
			Vector3 localScale = new Vector3(1f, 1f, 1f);
			Texture2D avatarImage = ClientUserSystem.GetAvatarImage(user);
			if (null == avatarImage)
			{
				m_GamerpicImage.sprite = m_gamerPicDefaultSprite;
			}
			else if (_force || avatarImage != m_lastAvatarImage)
			{
				m_GamerpicImage.sprite = Sprite.Create(avatarImage, new Rect(0f, 0f, avatarImage.width, avatarImage.height), new Vector2(0f, 0f), 100f, 1u, SpriteMeshType.FullRect);
			}
			m_lastAvatarImage = avatarImage;
			localScale.y = -1f;
			m_GamerpicImage.gameObject.transform.localScale = localScale;
			m_GamerpicImage.gameObject.SetActive(true);
		}
		else
		{
			m_GamerpicImage.gameObject.SetActive(false);
		}
	}

	private void SetSlotSelectedHighlight(bool slotEnabled, bool slotHasUser)
	{
		bool flag = slotEnabled && slotHasUser;
		bool flag2 = slotEnabled && !slotHasUser;
		if (m_frameImage != null)
		{
			m_frameImage.gameObject.SetActive(flag && !m_isSelected);
		}
		if (m_selectedFrameImage != null)
		{
			m_selectedFrameImage.gameObject.SetActive(flag && m_isSelected);
		}
		if (m_AddPlayerImage != null)
		{
			m_AddPlayerImage.gameObject.SetActive(flag2 && !m_isSelected);
		}
		if (m_selectedAddPlayerImage != null)
		{
			m_selectedAddPlayerImage.gameObject.SetActive(flag2 && m_isSelected);
		}
	}

	private void SetNameTextForUser(User user)
	{
		if (!(m_userNameText == null))
		{
			m_userNameText.gameObject.SetActive(user != null);
			if (user != null)
			{
				m_userNameText.SetNonLocalizedText(user.DisplayName);
			}
		}
	}
}
