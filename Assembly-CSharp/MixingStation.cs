using UnityEngine;

[ExecutionDependency(typeof(IFlowController))]
[AddComponentMenu("Scripts/Game/Environment/MixingStation")]
[RequireComponent(typeof(AttachStation))]
[RequireComponent(typeof(Flammable))]
public class MixingStation : MonoBehaviour
{
	[SerializeField]
	public Collider m_itemBlock;

	public bool m_bAttachRestrictions = true;
}
