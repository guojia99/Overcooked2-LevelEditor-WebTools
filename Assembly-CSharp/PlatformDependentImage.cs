using UnityEngine;
using UnityEngine.UI;

public class PlatformDependentImage : Image
{
	[SerializeField]
	private Sprite m_pcSprite;

	public Sprite PcSprite
	{
		get
		{
			return m_pcSprite;
		}
	}

	private Sprite PlatformSprite
	{
		get
		{
			return m_pcSprite;
		}
	}

	protected override void Awake()
	{
		if (Application.isPlaying)
		{
			base.sprite = PlatformSprite;
		}
		base.Awake();
	}
}
