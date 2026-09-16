using UnityEngine;

public class SpriteGUI : MonoBehaviour
{
	[SerializeField]
	private GUIRect m_rect;

	[SerializeField]
	private SubTexture2D m_texture;

	[SerializeField]
	private bool m_screenSpace;

	public void SetData(GUIRect _rect, SubTexture2D _texture, bool _screenSpace)
	{
		m_rect = _rect;
		m_texture = _texture;
		m_screenSpace = _screenSpace;
	}

	private void OnGUI()
	{
		Rect rect = ((!m_screenSpace) ? GUIUtils.ToPixels(m_rect, Camera.main, base.transform.position) : GUIUtils.ToPixels(m_rect, Camera.main));
		m_texture.Draw(rect);
	}
}
