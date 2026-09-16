using UnityEngine;
using UnityEngine.UI;

public class HoldDownToClose : MonoBehaviour
{
	[SerializeField]
	private GameObject m_ObjectToKill;

	[SerializeField]
	private float m_TimeToHoldDown = 1f;

	[SerializeField]
	private Image m_FillImage;

	private float m_HoldDownTimer;

	private ILogicalButton m_Button;

	private void Awake()
	{
		m_Button = PlayerInputLookup.GetUIButton(PlayerInputLookup.LogicalButtonID.UISelectNotStart);
		if (m_FillImage != null)
		{
			m_FillImage.fillAmount = 0f;
		}
	}

	private void Update()
	{
		if (m_Button.IsDown())
		{
			m_Button.ClaimPressEvent();
			m_HoldDownTimer += Time.deltaTime;
		}
		else
		{
			m_HoldDownTimer = 0f;
		}
		if (m_FillImage != null)
		{
			m_FillImage.fillAmount = m_HoldDownTimer / m_TimeToHoldDown;
		}
		if (m_HoldDownTimer >= m_TimeToHoldDown)
		{
			Object.Destroy(m_ObjectToKill);
		}
	}
}
