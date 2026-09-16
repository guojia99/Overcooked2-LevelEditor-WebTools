using UnityEngine;

[AddComponentMenu("Scripts/CosmeticDecisions/TravelatorCosmeticDecisions")]
[RequireComponent(typeof(Travelator))]
public class TravelatorCosmeticDecisions : MonoBehaviour
{
	private Travelator m_travelator;

	private void Start()
	{
		m_travelator = base.gameObject.RequireComponent<Travelator>();
	}
}
