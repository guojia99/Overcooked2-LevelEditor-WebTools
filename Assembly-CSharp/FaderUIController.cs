using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Image))]
public class FaderUIController : UIControllerBase
{
	[SerializeField]
	private bool m_startOpaque;

	[SerializeField]
	private bool m_fadeClearOnStart;

	[SerializeField]
	[AssignComponent(Editorbility.NonEditable)]
	private Image m_image;

	private static readonly int m_iStartOpaque = Animator.StringToHash("StartOpaque");

	private static readonly int m_iFadeIn = Animator.StringToHash("FadeIn");

	private void Awake()
	{
		Color color = m_image.color;
		color.a = ((!m_startOpaque) ? 0f : 1f);
		m_image.color = color;
		Animator animator = base.gameObject.RequireComponent<Animator>();
		animator.SetBool(m_iStartOpaque, m_startOpaque);
		if (m_fadeClearOnStart)
		{
			animator.SetTrigger(m_iFadeIn);
		}
	}
}
