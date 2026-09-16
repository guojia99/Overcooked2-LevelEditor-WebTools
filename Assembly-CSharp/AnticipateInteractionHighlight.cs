using UnityEngine;

[AddComponentMenu("Scripts/Game/Environment/AnticipateInteractionHighlight")]
internal class AnticipateInteractionHighlight : MonoBehaviour
{
	[SerializeField]
	public float m_brightnessModifier = 2f;

	[SerializeField]
	public GameObject m_highlightObjectOverride;
}
