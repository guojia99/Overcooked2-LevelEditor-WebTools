using UnityEngine;

public abstract class ScrollingListUIController : ScrollingListUIContainer, IUIController
{
	[SerializeField]
	private Vector2 m_offset;

	public Vector2 GetOffset()
	{
		return m_offset;
	}
}
