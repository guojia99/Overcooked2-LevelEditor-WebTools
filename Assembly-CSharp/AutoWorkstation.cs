using UnityEngine;

[AddComponentMenu("Scripts/Game/Environment/AutoWorkstation")]
public class AutoWorkstation : MonoBehaviour
{
	[SerializeField]
	[Range(1f, 16f)]
	public int m_choppingPower = 8;

	[SerializeField]
	public string m_workTrigger = string.Empty;

	[SerializeField]
	public string m_workFinishedTrigger = string.Empty;

	[SerializeField]
	public GameObject m_workFinishedTarget;
}
