using UnityEngine;

[RequireComponent(typeof(GridNavigator))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PlayerAttachmentCarrier))]
[RequireComponent(typeof(Interactable))]
public class RatBehaviour : MonoBehaviour
{
	[SerializeField]
	private int m_lives = 1;

	[SerializeField]
	private float m_knockbackDistance = 3f;
}
