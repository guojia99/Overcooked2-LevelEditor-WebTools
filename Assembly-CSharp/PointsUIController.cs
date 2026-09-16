using UnityEngine;

public class PointsUIController : DisplayIntUIController
{
	[SerializeField]
	private TeamID m_scoringTeam;

	private Animator m_animator;

	private static readonly int m_iScore = Animator.StringToHash("Score");

	public override int Value
	{
		get
		{
			return base.Value;
		}
		set
		{
			if (m_animator != null)
			{
				m_animator.SetTrigger(m_iScore);
			}
			base.Value = value;
		}
	}

	protected override void Awake()
	{
		base.Awake();
		m_animator = base.gameObject.RequireComponentRecursive<Animator>();
	}

	public TeamID GetTeam()
	{
		return m_scoringTeam;
	}
}
