using UnityEngine;

[RequireComponent(typeof(Interactable))]
public class ToggleSwitchCosmeticDecisions : MonoBehaviour
{
	[SerializeField]
	[AssignChildComponent(Editorbility.NonEditable)]
	public Animator m_animator;

	[SerializeField]
	[ReadOnly]
	public string m_stateParameter = "On";
}
