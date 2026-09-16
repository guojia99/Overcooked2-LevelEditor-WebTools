using UnityEngine;

[CreateAssetMenu(fileName = "DLCFrontendData", menuName = "Team17/DLC/Create Frontend Data")]
public class DLCFrontendData : ScriptableObject
{
	public string m_NameLocalizationKey;

	public string m_DescriptionLocalizationKey;

	public Sprite m_PreviewImage;

	public Sprite m_PopupImage;

	public int m_PopupOrder;

	[Space]
	public int m_DLCID;

	[SerializeField]
	private string m_XboxOneProductId = string.Empty;

	[SerializeField]
	private string m_PS4ProductId = string.Empty;

	[SerializeField]
	private string m_SteamProductId = string.Empty;

	[SerializeField]
	private string m_SwitchProductId = string.Empty;

	[SerializeField]
	private string m_GalaxyProductId = string.Empty;

	[SerializeField]
	private string m_GalaxyStoreURL = string.Empty;

	public DLCType m_type;

	public bool m_IsFreeDLC;

	public bool m_IsSeasonPassDLC;

	public bool m_ShowOnDlcPage = true;

	[Space]
	public bool m_InstallUnlocksAvatars;

	[Space]
	public bool m_PC = true;

	public bool m_PS4_SCEE = true;

	public bool m_PS4_SCEA = true;

	public bool m_PS4_SCEJ = true;

	public bool m_XboxOne = true;

	public bool m_Switch = true;

	public string productId
	{
		get
		{
			return m_SteamProductId;
		}
	}

	public string webpage
	{
		get
		{
			return string.Empty;
		}
	}

	public bool IsAvailableOnThisPlatform()
	{
		return m_PC;
	}
}
