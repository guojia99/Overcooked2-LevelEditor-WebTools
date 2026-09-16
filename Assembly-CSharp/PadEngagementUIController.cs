using System;
using UnityEngine;
using UnityEngine.UI;

public class PadEngagementUIController : UIControllerBase
{
	[SerializeField]
	private EngagementSlot m_engagementSlot;

	[SerializeField]
	[AssignComponent(Editorbility.Editable)]
	private Image m_background;

	[SerializeField]
	[AssignChild("FullPad", Editorbility.NonEditable)]
	private PlatformDependentImage m_fullPad;

	[SerializeField]
	[AssignChild("LeftPad", Editorbility.NonEditable)]
	private PlatformDependentImage m_leftPad;

	[SerializeField]
	[AssignChild("RightPad", Editorbility.NonEditable)]
	private PlatformDependentImage m_rightPad;

	[SerializeField]
	[AssignChild("ProfileName", Editorbility.NonEditable)]
	private Text m_profileName;

	[SerializeField]
	[AssignResource("MainAvatarDirectory", Editorbility.NonEditable)]
	private AvatarDirectoryData m_avatarDirectory;

	[SerializeField]
	private Sprite m_keyboardBothSprite;

	[SerializeField]
	private Sprite m_keyboardLeftSprite;

	[SerializeField]
	private Sprite m_keyboardRightSprite;

	[SerializeField]
	private Sprite m_nxProControllerSprite;

	[SerializeField]
	private Color m_greyedOutColour = new Color(0.5f, 0.5f, 0.5f, 1f);

	private Sprite m_restoreFullPadSprite;

	private Sprite m_restoreLeftPadSprite;

	private Sprite m_restoreRightPadSprite;

	private IPlayerManager m_iPlayerManager;

	private void Awake()
	{
		m_restoreFullPadSprite = m_fullPad.PcSprite;
		m_restoreLeftPadSprite = m_leftPad.PcSprite;
		m_restoreRightPadSprite = m_rightPad.PcSprite;
		m_iPlayerManager = GameUtils.RequireManagerInterface<IPlayerManager>();
		m_iPlayerManager.EngagementChangeCallback += OnEngagementChanged;
		PlayerInputLookup.OnRegenerateControls = (CallbackVoid)Delegate.Combine(PlayerInputLookup.OnRegenerateControls, new CallbackVoid(RefreshCosmetics));
		if (PlayerInputLookup.IsAwake())
		{
			RefreshCosmetics();
		}
	}

	private void OnDestroy()
	{
		PlayerInputLookup.OnRegenerateControls = (CallbackVoid)Delegate.Remove(PlayerInputLookup.OnRegenerateControls, new CallbackVoid(RefreshCosmetics));
		m_iPlayerManager.EngagementChangeCallback -= OnEngagementChanged;
	}

	private void OnEngagementChanged(EngagementSlot _e, GamepadUser _before, GamepadUser _new)
	{
		if (PlayerInputLookup.IsAwake())
		{
			RefreshCosmetics();
		}
	}

	private void RefreshCosmetics()
	{
		m_fullPad.gameObject.SetActive(false);
		m_leftPad.gameObject.SetActive(false);
		m_rightPad.gameObject.SetActive(false);
		m_profileName.gameObject.SetActive(false);
		m_background.enabled = true;
		GamepadUser user = m_iPlayerManager.GetUser(m_engagementSlot);
		if (user != null)
		{
			GameInputConfig baseInputConfig = PlayerInputLookup.GetBaseInputConfig();
			GameInputConfig.ConfigEntry[] array = baseInputConfig.m_playerConfigs.FindAll((GameInputConfig.ConfigEntry x) => x.Pad == (ControlPadInput.PadNum)m_engagementSlot);
			for (int num = 0; num < array.Length; num++)
			{
				PadSide uIHandedness = array[num].UIHandedness;
				Image image = GetImage(uIHandedness);
				image.gameObject.SetActive(true);
				ChefColourData chefColourData = m_avatarDirectory.Colours[(int)array[num].Player];
				image.color = chefColourData.PadUIColour;
			}
			m_profileName.gameObject.SetActive(true);
			m_profileName.text = Ellipsify(user.DisplayName);
			bool flag = user.ControlType == GamepadUser.ControlTypeEnum.Pad;
			m_fullPad.sprite = ((!flag) ? m_keyboardBothSprite : m_restoreFullPadSprite);
			m_leftPad.sprite = ((!flag) ? m_keyboardLeftSprite : m_restoreLeftPadSprite);
			m_rightPad.sprite = ((!flag) ? m_keyboardRightSprite : m_restoreRightPadSprite);
			m_background.enabled = flag;
		}
		else
		{
			m_background.enabled = false;
			m_fullPad.gameObject.SetActive(true);
			m_fullPad.color = m_greyedOutColour;
		}
	}

	private Image GetImage(PadSide _side)
	{
		switch (_side)
		{
		case PadSide.Both:
			return m_fullPad;
		case PadSide.Left:
			return m_leftPad;
		case PadSide.Right:
			return m_rightPad;
		default:
			return null;
		}
	}

	private string Ellipsify(string _input)
	{
		if (_input.Length <= 16)
		{
			return _input;
		}
		return _input.Substring(0, 16) + "...";
	}
}
