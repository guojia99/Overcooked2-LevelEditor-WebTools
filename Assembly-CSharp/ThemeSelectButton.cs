using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(T17Button))]
public class ThemeSelectButton : CarouselButton
{
	[SerializeField]
	private Image m_themeImage;

	[SerializeField]
	private SceneDirectoryData.LevelTheme m_theme;

	public Sprite ThemeSprite
	{
		get
		{
			if (m_themeImage != null)
			{
				return m_themeImage.sprite;
			}
			return null;
		}
	}

	public SceneDirectoryData.LevelTheme Theme
	{
		get
		{
			return m_theme;
		}
	}
}
