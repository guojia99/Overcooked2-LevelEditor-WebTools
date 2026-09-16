using UnityEngine;

[RequireComponent(typeof(TriggerZone))]
public class PressureSwitchCosmeticDecisions : MonoBehaviour
{
	[SerializeField]
	public Material m_occupiedMaterial;

	[SerializeField]
	public Material m_unoccuppiedMaterial;

	[SerializeField]
	public Renderer m_buttonBit;

	[SerializeField]
	public float m_occupiedButtonVerticalOffset;
}
