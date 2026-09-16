using UnityEngine;

public class TextTicker : MonoBehaviour
{
	public T17Text m_Text;

	public char m_TickerCharacter = '.';

	private int m_TickCount;

	public int m_MaxTickCount;

	private float m_CurrentTickTimer;

	public float m_TickLength;

	private void Update()
	{
		if (!(m_Text != null))
		{
			return;
		}
		float time = Time.time;
		if (m_CurrentTickTimer + m_TickLength < time)
		{
			m_TickCount++;
			if (m_TickCount > m_MaxTickCount)
			{
				m_TickCount = 0;
			}
			string text = string.Empty;
			for (int i = 0; i < m_TickCount; i++)
			{
				text += m_TickerCharacter;
			}
			m_Text.SetNonLocalizedText(text);
			m_CurrentTickTimer = time;
		}
	}
}
