using UnityEngine;

[AddComponentMenu("Scripts/Game/Environment/Interactable")]
public class Interactable : MonoBehaviour
{
	[SerializeField]
	public string m_onInteractStartedTrigger = string.Empty;

	[SerializeField]
	public string m_onInteractEndedTrigger = string.Empty;

	[SerializeField]
	public string m_onInteractImpulseTrigger = string.Empty;

	[SerializeField]
	public bool m_allowMultipleInteracters;

	[SerializeField]
	public bool m_usePlacementButton;
}
