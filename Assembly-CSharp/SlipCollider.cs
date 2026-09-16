using UnityEngine;

[RequireComponent(typeof(CollisionRecorder))]
public class SlipCollider : MonoBehaviour
{
	[SerializeField]
	public LayerMask m_slipFilter = -1;

	[SerializeField]
	public bool m_deleteOnSlip = true;
}
