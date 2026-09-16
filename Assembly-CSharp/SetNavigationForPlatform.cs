using UnityEngine;
using UnityEngine.UI;

internal class SetNavigationForPlatform : MonoBehaviour
{
	[SerializeField]
	private Selectable m_SelectableToFix;

	[SerializeField]
	private Navigation m_NavigationToSet;

	[Mask(typeof(PlatformUtils.Platforms))]
	public int m_platformsActiveOn;

	private void Awake()
	{
		if (m_SelectableToFix != null && PlatformUtils.HasPlatformFlag(m_platformsActiveOn))
		{
			m_SelectableToFix.navigation = m_NavigationToSet;
		}
	}
}
