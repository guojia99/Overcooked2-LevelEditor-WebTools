using UnityEngine;

public class HeatedCookingStation : CookingStation
{
	[Space]
	[AssignComponent(Editorbility.Editable)]
	[SerializeField]
	public HeatedStation m_heatSource;

	[Range(0f, 1f)]
	[SerializeField]
	public float m_cookingSpeedHigh = 1f;

	[Range(0f, 1f)]
	[SerializeField]
	public float m_cookingSpeedModerate = 0.66f;

	[Range(0f, 1f)]
	[SerializeField]
	public float m_cookingSpeedLow = 0.33f;
}
