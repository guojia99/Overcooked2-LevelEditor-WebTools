using UnityEngine;
using UnityEngine.UI;

public abstract class StarRatingUIController : UIControllerBase
{
	[SerializeField]
	private Image m_buttonIcon;

	private Animator m_animator;

	private bool m_bScoreSet;

	private static readonly int m_iStarRating = Animator.StringToHash("StarRating");

	private static readonly int m_iReady = Animator.StringToHash("Ready");

	protected virtual void Awake()
	{
		m_animator = base.gameObject.RequireComponent<Animator>();
		if (m_buttonIcon != null)
		{
			m_buttonIcon.enabled = false;
		}
	}

	public abstract void SetScoreData(object _data);

	protected void SetScoreData(int _starRating)
	{
		m_animator.SetInteger(m_iStarRating, _starRating);
		m_bScoreSet = true;
	}

	public void SetButtonActive()
	{
		if (m_buttonIcon != null)
		{
			m_buttonIcon.enabled = true;
		}
	}

	public bool HasAnimationSettled()
	{
		int integer = m_animator.GetInteger(m_iStarRating);
		AnimatorStateInfo currentAnimatorStateInfo = m_animator.GetCurrentAnimatorStateInfo(1);
		return m_bScoreSet && currentAnimatorStateInfo.IsName("Star" + integer) && m_animator.GetBool(m_iReady);
	}

	public virtual bool AllowedToSkip()
	{
		return true;
	}

	public virtual bool AllowedToRestart()
	{
		return true;
	}
}
