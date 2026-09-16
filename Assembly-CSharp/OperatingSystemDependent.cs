using UnityEngine;

internal class OperatingSystemDependent : MonoBehaviour
{
	[Mask(typeof(PlatformUtils.OperatingSystem))]
	public int m_operatingSystemActiveOn;

	private void Awake()
	{
		if (!PlatformUtils.HasOperatingSystemFlag(m_operatingSystemActiveOn))
		{
			base.gameObject.SetActive(false);
		}
	}
}
