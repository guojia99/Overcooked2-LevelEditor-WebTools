using UnityEngine;

public class PlatformDependent : MonoBehaviour
{
	[Mask(typeof(PlatformUtils.Platforms))]
	public int m_platformsActiveOn;

	private void Awake()
	{
		if (!PlatformUtils.HasPlatformFlag(m_platformsActiveOn))
		{
			base.gameObject.SetActive(false);
		}
	}
}
