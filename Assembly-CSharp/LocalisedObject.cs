using UnityEngine;

public class LocalisedObject : MonoBehaviour
{
	private enum FlagBehaviour
	{
		Activator = 0,
		Deactivator = 1
	}

	[SerializeField]
	[Mask(typeof(SupportedLanguages))]
	private int m_languageMask;

	[SerializeField]
	private FlagBehaviour m_flagBehaviour;

	protected void Awake()
	{
		SupportedLanguages language = Localization.GetLanguage();
		bool flag = MaskUtils.HasFlag(m_languageMask, language);
		bool flag2 = m_flagBehaviour == FlagBehaviour.Activator;
		bool active = !(flag ^ flag2);
		base.gameObject.SetActive(active);
	}
}
