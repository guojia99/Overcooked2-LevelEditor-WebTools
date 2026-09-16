using UnityEngine;
using UnityEngine.UI;

public class T17CounterObject : MonoBehaviour
{
	public delegate void T17CounterObjectDelegate();

	public Text m_CounterDisplay;

	public int m_Digits = 3;

	public int m_MinimumValue;

	public int m_MaxValue = 999;

	public int m_StartValue = 1;

	public T17CounterObjectDelegate OnCounterIncreased;

	public T17CounterObjectDelegate OnCounterDescreased;

	public T17CounterObjectDelegate OnCounterMinumumValueReached;

	public T17CounterObjectDelegate OnCounterMaxValueReached;

	private int m_CounterValue;

	private void Start()
	{
		m_StartValue = Mathf.Clamp(m_StartValue, m_MinimumValue, m_MaxValue);
		m_CounterValue = m_StartValue;
		UpdateText();
	}

	public void CounterIncrease()
	{
		m_CounterValue++;
		if (m_CounterValue > m_MaxValue)
		{
			m_CounterValue = m_MaxValue;
		}
		UpdateText();
		if (OnCounterIncreased != null)
		{
			OnCounterIncreased();
		}
		if (m_CounterValue == m_MaxValue && OnCounterMaxValueReached != null)
		{
			OnCounterMaxValueReached();
		}
	}

	public void CounterDescrease()
	{
		m_CounterValue--;
		if (m_CounterValue < m_MinimumValue)
		{
			m_CounterValue = m_MinimumValue;
		}
		if (OnCounterDescreased != null)
		{
			OnCounterDescreased();
		}
		if (m_CounterValue == m_MinimumValue && OnCounterMinumumValueReached != null)
		{
			OnCounterMinumumValueReached();
		}
		UpdateText();
	}

	private void UpdateText()
	{
		string text = m_CounterValue.ToString().PadLeft(m_Digits, '0');
		if (m_CounterDisplay != null)
		{
			m_CounterDisplay.text = text;
		}
	}

	public void SetMaxValue(int maxValue)
	{
		m_MaxValue = Mathf.Max(m_MinimumValue, maxValue);
		m_CounterValue = Mathf.Clamp(m_CounterValue, m_MinimumValue, m_MaxValue);
		UpdateText();
	}

	public void Reset()
	{
		m_StartValue = Mathf.Clamp(m_StartValue, m_MinimumValue, m_MaxValue);
		m_CounterValue = m_StartValue;
		UpdateText();
	}

	public int GetCounterValue()
	{
		return m_CounterValue;
	}
}
