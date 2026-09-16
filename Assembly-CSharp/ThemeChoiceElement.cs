using UnityEngine;

public class ThemeChoiceElement : MonoBehaviour
{
	[SerializeField]
	private T17Image m_image;

	[SerializeField]
	private T17Image m_frameImage;

	private SceneDirectoryData.LevelTheme m_themeInfo;

	[SerializeField]
	private Sprite m_notSelectedSprite;

	[SerializeField]
	private Sprite m_selectedSprite;

	protected bool m_bIsInitialised;

	public SceneDirectoryData.LevelTheme Theme
	{
		get
		{
			return m_themeInfo;
		}
		set
		{
			m_themeInfo = value;
		}
	}

	public Sprite Sprite
	{
		set
		{
			m_image.sprite = value;
			m_image.enabled = value != null;
		}
	}

	private void Start()
	{
		m_image.enabled = m_image.sprite != null;
	}

	public void ShowAsSelected(bool _isSelected)
	{
		m_frameImage.sprite = ((!_isSelected) ? m_notSelectedSprite : m_selectedSprite);
	}
}
