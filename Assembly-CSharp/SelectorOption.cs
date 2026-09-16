using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class SelectorOption : BaseUIOption<INameListOption>
{
	[SerializeField]
	private T17Button m_ButtonToHaveFocusOnForLeftRight;

	[SerializeField]
	private T17Button m_LeftButton;

	[SerializeField]
	private T17Button m_RightButton;

	[SerializeField]
	private T17Text m_ValueText;

	private FrontendOptionsMenu m_optionsMenu;

	[SerializeField]
	private bool m_LocalizeOption = true;

	private ILogicalButton m_LeftInput;

	private ILogicalButton m_RightInput;

	protected override void Awake()
	{
		base.Awake();
		if (m_LeftButton != null)
		{
			m_LeftButton.onClick.AddListener(OnLeftPressed);
		}
		if (m_RightButton != null)
		{
			m_RightButton.onClick.AddListener(OnRightPressed);
		}
		m_LeftInput = PlayerInputLookup.GetUIButton(PlayerInputLookup.LogicalButtonID.UILeft);
		m_RightInput = PlayerInputLookup.GetUIButton(PlayerInputLookup.LogicalButtonID.UIRight);
		m_optionsMenu = base.gameObject.RequestComponentUpwardsRecursive<FrontendOptionsMenu>();
	}

	private void Update()
	{
		if (!(m_ButtonToHaveFocusOnForLeftRight != null) || !(m_ButtonToHaveFocusOnForLeftRight.GetDomain() != null))
		{
			return;
		}
		Navigation navigation = m_ButtonToHaveFocusOnForLeftRight.navigation;
		navigation.selectOnLeft = null;
		navigation.selectOnRight = null;
		m_ButtonToHaveFocusOnForLeftRight.navigation = navigation;
		if (m_ButtonToHaveFocusOnForLeftRight.GetDomain().currentSelectedGameObject == m_ButtonToHaveFocusOnForLeftRight.gameObject)
		{
			if (m_LeftInput.JustPressed())
			{
				m_LeftInput.ClaimPressEvent();
				OnLeftPressed();
			}
			else if (m_RightInput.JustPressed())
			{
				m_RightInput.ClaimPressEvent();
				OnRightPressed();
			}
		}
	}

	public override void SyncUIWithOption()
	{
		if (m_Option != null && m_ValueText != null)
		{
			if (m_LocalizeOption)
			{
				m_ValueText.SetLocalisedTextCatchAll(m_Option.GetNames()[m_Option.GetOption()]);
			}
			else
			{
				m_ValueText.SetNonLocalizedText(m_Option.GetNames()[m_Option.GetOption()]);
			}
		}
	}

	private void OnLeftPressed()
	{
		int num = m_Option.GetOption() - 1;
		string[] names = m_Option.GetNames();
		if (num < 0)
		{
			num = names.Length - 1;
		}
		m_Option.SetOption(num);
		GameUtils.TriggerAudio(GameOneShotAudioTag.UIHighlight, base.gameObject.layer);
		StartCoroutine(UpdateUIAtEndOfFrame());
	}

	private void OnRightPressed()
	{
		int num = m_Option.GetOption() + 1;
		string[] names = m_Option.GetNames();
		if (num >= names.Length)
		{
			num = 0;
		}
		m_Option.SetOption(num);
		GameUtils.TriggerAudio(GameOneShotAudioTag.UIHighlight, base.gameObject.layer);
		StartCoroutine(UpdateUIAtEndOfFrame());
	}

	private IEnumerator UpdateUIAtEndOfFrame()
	{
		yield return new WaitForEndOfFrame();
		yield return new WaitForEndOfFrame();
		if (m_optionsMenu != null)
		{
			m_optionsMenu.SyncAllOptions();
		}
	}
}
