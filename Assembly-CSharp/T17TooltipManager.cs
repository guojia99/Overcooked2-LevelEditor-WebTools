using UnityEngine;

public class T17TooltipManager : MonoBehaviour
{
	private static T17TooltipManager s_Instance;

	public Transform m_TooltipCanvas;

	public GameObject m_TooltipPrefab;

	private T17Tooltip m_Tooltip;

	private string m_DefaultTooltip = "Text.Menu.DefaultTooltip";

	private LocToken[] m_defaultReplacements;

	private bool m_bIsSetup;

	public static T17TooltipManager Instance
	{
		get
		{
			return s_Instance;
		}
	}

	private void Awake()
	{
		if (s_Instance != null)
		{
			Object.Destroy(this);
		}
		else
		{
			s_Instance = this;
		}
	}

	private void OnDestroy()
	{
		if (s_Instance == this)
		{
			s_Instance = null;
		}
	}

	private void Start()
	{
		if (!m_bIsSetup)
		{
			Setup();
		}
	}

	private void Setup()
	{
		if (m_TooltipPrefab != null && m_TooltipCanvas != null)
		{
			GameObject gameObject = Object.Instantiate(m_TooltipPrefab);
			gameObject.transform.SetParent(m_TooltipCanvas, false);
			gameObject.SetActive(true);
			m_Tooltip = gameObject.GetComponent<T17Tooltip>();
			m_Tooltip.m_Text.m_LocalizationTag = m_DefaultTooltip;
			m_Tooltip.m_Text.Convert();
			m_bIsSetup = true;
		}
	}

	public void SetDefaultTooltip(string _tag, LocToken[] _replacements = null)
	{
		m_DefaultTooltip = _tag;
		m_defaultReplacements = _replacements;
	}

	public void Show(string message, bool isLocalised = false)
	{
		if (!m_bIsSetup)
		{
			Setup();
		}
		if (!isLocalised)
		{
			message = Localization.Get(message);
		}
		if (!string.IsNullOrEmpty(message))
		{
			m_Tooltip.m_Text.SetNonLocalizedText(message);
		}
		else if (m_defaultReplacements != null)
		{
			m_Tooltip.m_Text.SetNonLocalizedText(Localization.Get(m_DefaultTooltip, m_defaultReplacements));
		}
		else
		{
			m_Tooltip.m_Text.SetNonLocalizedText(Localization.Get(m_DefaultTooltip));
		}
		if (!m_Tooltip.gameObject.activeInHierarchy)
		{
			m_Tooltip.gameObject.SetActive(true);
		}
	}
}
