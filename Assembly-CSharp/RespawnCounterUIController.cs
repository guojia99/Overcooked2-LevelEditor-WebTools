using UnityEngine;
using UnityEngine.UI;

public class RespawnCounterUIController : HoverIconUIController
{
	[SerializeField]
	private Text m_textUI;

	[SerializeField]
	private string m_matchString;

	private Image m_background;

	private TeamID m_team;

	private int m_playerNum;

	[SerializeField]
	private Sprite[] m_backgrounds;

	private string m_originalText;

	private float m_countDown;

	private GameObject m_target;

	private void Start()
	{
		m_background = base.gameObject.RequestComponent<Image>();
	}

	public void SetTarget(GameObject _target)
	{
		m_target = _target;
	}

	public void SetCountdown(float _countDown)
	{
		m_countDown = _countDown;
	}

	public void SetTeam(TeamID _team)
	{
		m_team = _team;
	}

	public void SetPlayerNum(int _num)
	{
		m_playerNum = _num;
	}

	protected override void Awake()
	{
		base.Awake();
		m_originalText = m_textUI.text;
	}

	public override void LateUpdate()
	{
		m_countDown -= TimeManager.GetDeltaTime((!(m_target != null)) ? base.gameObject : m_target);
		int num = Mathf.Max(Mathf.CeilToInt(m_countDown), 0);
		m_textUI.text = m_originalText.Replace(m_matchString, num.ToString());
		if (m_background != null)
		{
			if (m_team == TeamID.None)
			{
				m_background.sprite = m_backgrounds[m_playerNum];
			}
			else
			{
				m_background.sprite = m_backgrounds[(int)(1 - m_team)];
			}
		}
		base.LateUpdate();
	}
}
