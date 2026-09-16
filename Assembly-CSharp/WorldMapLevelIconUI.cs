using UnityEngine;

[RequireComponent(typeof(Animator))]
public class WorldMapLevelIconUI : HoverIconUIController
{
	[SerializeField]
	private T17Text[] m_titles;

	[SerializeField]
	private GameObject m_attentionPopup;

	protected Animator m_animator;

	private static readonly int m_iPlayerClose = Animator.StringToHash("PlayerClose");

	protected override void Awake()
	{
		base.Awake();
		m_animator = base.gameObject.RequireComponent<Animator>();
	}

	public bool IsReady()
	{
		return m_animator.IsActive();
	}

	public void SetTitle(string _title)
	{
		for (int i = 0; i < m_titles.Length; i++)
		{
			m_titles[i].SetLocalisedTextCatchAll(_title);
		}
	}

	public void SetAvatarProximity(bool _inProximity)
	{
		m_animator.SetBool(m_iPlayerClose, _inProximity);
	}

	public void ActivateAttentionPopup()
	{
		m_attentionPopup.SetActive(true);
	}
}
