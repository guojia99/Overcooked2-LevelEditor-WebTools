using UnityEngine;

public class HeatedStationGUI : MonoBehaviour
{
	[SerializeField]
	public HeatValueUIController m_heatUIPrefab;

	[SerializeField]
	public bool m_displayWhenCold;

	[SerializeField]
	public Vector3 m_Offset = Vector3.zero;
}
