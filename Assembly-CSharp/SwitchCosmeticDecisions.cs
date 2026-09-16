using UnityEngine;

[RequireComponent(typeof(Interactable))]
public class SwitchCosmeticDecisions : MonoBehaviour
{
	[SerializeField]
	public Material m_activeMaterial;

	[SerializeField]
	public Material m_inactiveMaterial;

	[SerializeField]
	public Renderer m_buttonBit;
}
