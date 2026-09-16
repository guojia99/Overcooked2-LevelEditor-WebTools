using UnityEngine;

public class TimedInputUIController : UIControllerBase
{
	[SerializeField]
	[AssignComponentRecursive(Editorbility.NonEditable)]
	private T17FilledImage m_progressBar;

	private ILogicalButton m_button;

	private float m_targetTime;

	public void SetDisplayInput(TimedLogicalButton _button, float _targetTime)
	{
		m_button = _button;
		m_targetTime = _targetTime;
		Update();
	}

	protected void OnEnable()
	{
		UpdateProgress();
	}

	public void Update()
	{
		UpdateProgress();
	}

	private void UpdateProgress()
	{
		if (m_button == null)
		{
			if (base.gameObject.activeSelf)
			{
				base.gameObject.SetActive(false);
			}
			return;
		}
		float filledAmount = Mathf.Clamp01(m_button.GetHeldTimeLength() / m_targetTime);
		m_progressBar.SetFilledAmount(filledAmount);
		if (!base.gameObject.activeSelf)
		{
			base.gameObject.SetActive(true);
		}
	}
}
