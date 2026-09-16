using UnityEngine;

public class MultiplierUIController : DisplayIntUIController
{
	[SerializeField]
	private TeamID m_scoringTeam;

	[SerializeField]
	private Animator m_animator;

	[SerializeField]
	private string m_LocalizedTextID = "Text.HUD.TipMultiplier";

	private static readonly int m_iMultiplier = Animator.StringToHash("Multiplier");

	public override int Value
	{
		set
		{
			m_value = value;
			string text = Localization.Get(m_LocalizedTextID, new LocToken("Multiplier", m_value.ToString()));
			m_textUI.text = text;
			m_animator.SetTrigger(m_iMultiplier);
		}
	}

	public TeamID GetTeam()
	{
		return m_scoringTeam;
	}

	protected override void Awake()
	{
		m_value = 0;
		base.Awake();
	}
}
