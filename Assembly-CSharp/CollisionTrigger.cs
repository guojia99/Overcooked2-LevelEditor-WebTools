using UnityEngine;

public class CollisionTrigger : MonoBehaviour
{
	[SerializeField]
	public LayerMask m_collisionFilter = -1;

	[SerializeField]
	public string m_trigger;
}
