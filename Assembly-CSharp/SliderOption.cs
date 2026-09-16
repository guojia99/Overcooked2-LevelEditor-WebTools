using UnityEngine;
using UnityEngine.UI;

public class SliderOption : BaseUIOption<IQuantizedOption>
{
	[SerializeField]
	private T17Image[] m_StepImages;

	[SerializeField]
	private Sprite m_EnabledSprite;

	[SerializeField]
	private Sprite m_DisabledSprite;

	[SerializeField]
	private T17Button m_ButtonToHaveFocusOnForLeftRight;

	[SerializeField]
	private T17Button m_LeftButton;

	[SerializeField]
	private T17Button m_RightButton;

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
		if (m_Option == null)
		{
			return;
		}
		int option = m_Option.GetOption();
		option = Mathf.Clamp(option, 0, m_StepImages.Length);
		for (int i = 0; i < m_StepImages.Length; i++)
		{
			if (i < option)
			{
				m_StepImages[i].sprite = m_EnabledSprite;
			}
			else
			{
				m_StepImages[i].sprite = m_DisabledSprite;
			}
		}
	}

	private void OnLeftPressed()
	{
		int value = m_Option.GetOption() - 1;
		value = Mathf.Clamp(value, 0, m_Option.Quanta);
		m_Option.SetOption(value);
		GameUtils.TriggerAudio(GameOneShotAudioTag.UIHighlight, base.gameObject.layer);
		SyncUIWithOption();
	}

	private void OnRightPressed()
	{
		int value = m_Option.GetOption() + 1;
		value = Mathf.Clamp(value, 0, m_Option.Quanta);
		m_Option.SetOption(value);
		GameUtils.TriggerAudio(GameOneShotAudioTag.UIHighlight, base.gameObject.layer);
		SyncUIWithOption();
	}
}
