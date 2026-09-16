using UnityEngine;

public class Washable : MonoBehaviour
{
	[SerializeField]
	public ProgressUIController m_progressUIPrefab;

	[SerializeField]
	public int m_duration = 1;

	public int GetWashTimeMultiplier()
	{
		return 1;
	}
}
