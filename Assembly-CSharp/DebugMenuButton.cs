using UnityEngine;

public class DebugMenuButton : MonoBehaviour
{
	[SerializeField]
	private T17Text m_nameText;

	[SerializeField]
	private T17Text m_statusText;

	private bool m_status;

	public void SetName(string name)
	{
		m_nameText.text = name;
	}

	public void SetStatus(bool value)
	{
		m_status = value;
		m_statusText.text = m_status.ToString();
	}

	public void Clicked()
	{
	}
}
