using UnityEngine;

public class DisplayTimeUIController : DisplayIntUIController
{
	private const int k_timeMax = 5999;

	public override int Value
	{
		get
		{
			return m_value;
		}
		set
		{
			m_value = Mathf.Clamp(Mathf.CeilToInt(value), 0, 5999);
			m_textUI.text = m_value.ToTimeString();
		}
	}
}
