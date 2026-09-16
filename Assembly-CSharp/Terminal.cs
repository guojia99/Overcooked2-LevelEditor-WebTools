using UnityEngine;

[RequireComponent(typeof(Interactable))]
public class Terminal : SessionInteractable
{
	[SerializeField]
	public PilotMovement m_pilotableObject;
}
