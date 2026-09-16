using UnityEngine;
using UnityEngine.UI;

[ExecutionDependency(typeof(LocalisedText))]
public class DisplayIntUIController : UIControllerBase
{
	[SerializeField]
	[AssignComponentRecursive(Editorbility.NonEditable)]
	protected Text m_textUI;

	[SerializeField]
	protected string m_matchString;

	protected string m_originalText;

	protected int m_value;

	public virtual int Value
	{
		get
		{
			return m_value;
		}
		set
		{
			m_value = value;
			string text = m_originalText.Replace(m_matchString, m_value.ToString());
			if (m_textUI as LocalisedText != null)
			{
				LocalisedText localisedText = m_textUI as LocalisedText;
				localisedText.literalText = text;
			}
			else
			{
				m_textUI.text = text;
			}
		}
	}

	protected virtual void Awake()
	{
		if (m_textUI == null)
		{
			m_textUI = base.gameObject.RequireComponentRecursive<Text>();
		}
		m_originalText = m_textUI.text;
		Value = m_value;
	}
}
