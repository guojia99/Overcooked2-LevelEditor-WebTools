using UnityEngine;

public class AttachmentThrower : MonoBehaviour
{
	[Range(0f, 90f)]
	[SerializeField]
	public float m_throwInclination;

	[SerializeField]
	public float m_throwForce;
}
