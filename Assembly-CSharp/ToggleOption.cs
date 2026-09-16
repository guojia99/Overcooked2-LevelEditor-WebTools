using UnityEngine;

public class ToggleOption : BaseUIOption<IOption>
{
	[SerializeField]
	private T17Toggle m_Toggle;

	protected override void Awake()
	{
		base.Awake();
		SyncUIWithOption();
		m_Toggle.onValueChanged.AddListener(OnToggleButtonPressed);
	}

	public override void SyncUIWithOption()
	{
		if (m_Toggle != null && m_Option != null)
		{
			m_Toggle.isOn = m_Option.GetOption() == 1;
		}
	}

	private void OnToggleButtonPressed(bool bValue)
	{
		int option = (bValue ? 1 : 0);
		m_Option.SetOption(option);
		GameUtils.TriggerAudio(GameOneShotAudioTag.UISelect, base.gameObject.layer);
	}
}
