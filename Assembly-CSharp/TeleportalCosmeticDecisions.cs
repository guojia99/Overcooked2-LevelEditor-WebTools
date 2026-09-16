using UnityEngine;

[AddComponentMenu("Scripts/CosmeticDecisions/TeleportalCosmeticDecisions")]
public class TeleportalCosmeticDecisions : MonoBehaviour
{
	[SerializeField]
	public Animator m_portalAnimator;

	[SerializeField]
	public string m_portalTeleportTrigger = string.Empty;
}
