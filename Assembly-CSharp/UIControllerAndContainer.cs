using UnityEngine;

public class UIControllerAndContainer : UISubElementContainer, IUIController
{
	[SerializeField]
	private Vector2 m_offset;

	public Vector2 GetOffset()
	{
		return m_offset;
	}
}
