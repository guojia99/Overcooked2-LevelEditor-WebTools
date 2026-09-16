using UnityEngine;

[RequireComponent(typeof(PhysicalAttachment))]
[RequireComponent(typeof(Collider))]
public class ThrowableItem : MonoBehaviour
{
	[SerializeField]
	public GameObject m_throwParticle;

	[SerializeField]
	public float m_throwerTimeout = 2f;
}
