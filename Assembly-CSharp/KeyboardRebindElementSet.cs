using UnityEngine;

public class KeyboardRebindElementSet : MonoBehaviour
{
	private KeyboardRebindElement[] m_Elements;

	public int ElementCount
	{
		get
		{
			return (m_Elements != null) ? m_Elements.Length : 0;
		}
	}

	public KeyboardRebindElement this[int i]
	{
		get
		{
			return (m_Elements == null) ? null : m_Elements[i];
		}
	}

	private void Awake()
	{
		m_Elements = GetComponentsInChildren<KeyboardRebindElement>();
	}
}
