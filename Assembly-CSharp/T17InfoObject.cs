using UnityEngine;
using UnityEngine.UI;

public class T17InfoObject : MonoBehaviour
{
	public Image m_InfoIcon;

	public T17Text m_Title;

	public T17Text m_Value;

	public T17Slider m_Slider;

	private int m_PreviousRawValue = -1;

	private float m_PreviousMaxValue = -1f;

	public void SetImage(Sprite image)
	{
		if (m_InfoIcon != null)
		{
			m_InfoIcon.sprite = image;
		}
	}

	public void SetTitle(string title)
	{
		if (m_Title != null)
		{
			m_Title.SetNewPlaceHolder(title);
			m_Title.m_bNeedsLocalization = false;
			m_Title.text = title;
		}
	}

	public void SetValue(string value)
	{
		if (m_Value != null)
		{
			m_Value.SetNewPlaceHolder(value);
			m_Value.m_bNeedsLocalization = false;
			m_Value.text = value;
		}
	}

	public Sprite GetImage()
	{
		if (m_InfoIcon != null)
		{
			return m_InfoIcon.sprite;
		}
		return null;
	}

	public string GetTitle()
	{
		if (m_Title != null)
		{
			return m_Title.text;
		}
		return string.Empty;
	}

	public string GetValue()
	{
		if (m_Value != null)
		{
			return m_Value.text;
		}
		return string.Empty;
	}

	public void SetSliderProgress(float ratio)
	{
		if (m_Slider != null)
		{
			m_Slider.value = (m_Slider.maxValue - m_Slider.minValue) * ratio + m_Slider.minValue;
		}
	}

	public void SetValues(int rawValue, int maxValue)
	{
		SetValues(rawValue, (float)maxValue);
	}

	public void SetValues(int rawValue, float maxValue)
	{
		if (rawValue != m_PreviousRawValue || maxValue != m_PreviousMaxValue)
		{
			m_PreviousRawValue = rawValue;
			m_PreviousMaxValue = maxValue;
			SetValue(rawValue.ToString());
			SetSliderProgress((float)rawValue / maxValue);
		}
	}
}
