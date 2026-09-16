using UnityEngine;
using UnityEngine.UI;

public class DisplayMultiplerUIController : MonoBehaviour
{
	[SerializeField]
	[AssignComponentRecursive(Editorbility.NonEditable)]
	private Text m_textUI;

	[SerializeField]
	private int m_first;

	[SerializeField]
	private int m_second = 20;

	public virtual int FirstValue
	{
		get
		{
			return m_first;
		}
		set
		{
			m_first = value;
			RefreshText();
		}
	}

	public virtual int SecondValue
	{
		get
		{
			return m_second;
		}
		set
		{
			m_second = value;
			RefreshText();
		}
	}

	private void RefreshText()
	{
		int num = m_first * m_second;
		m_textUI.text = m_first + " x " + m_second + " = " + num;
	}

	protected virtual void Awake()
	{
		if (m_textUI == null)
		{
			m_textUI = base.gameObject.RequireComponent<Text>();
		}
		RefreshText();
	}
}
