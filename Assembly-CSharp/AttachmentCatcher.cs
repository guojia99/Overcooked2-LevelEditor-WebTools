using UnityEngine;

[RequireComponent(typeof(PlayerAttachmentCarrier))]
public class AttachmentCatcher : MonoBehaviour
{
	[Range(0f, 360f)]
	[SerializeField]
	public float m_catchAngleMax;

	[SerializeField]
	public float m_catchDistance;
}
