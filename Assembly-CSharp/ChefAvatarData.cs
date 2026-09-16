using UnityEngine;

public class ChefAvatarData : ScriptableObject
{
	public GameObject ModelPrefab;

	public GameObject FrontendModelPrefab;

	public GameObject UIModelPrefab;

	public string HeadName;

	[Space]
	public ChefMeshReplacer.ChefColourisationMode ColourisationMode;

	[Header("Legacy")]
	public bool ActuallyAllowed = true;

	[Header("For DLC")]
	public DLCFrontendData ForDlc;

	[Header("For Platform")]
	public bool m_PC = true;

	public bool m_XboxOne = true;

	public bool m_PS4 = true;

	public bool m_Switch = true;

	public bool IsAvailableOnThisPlatform()
	{
		return m_PC;
	}
}
