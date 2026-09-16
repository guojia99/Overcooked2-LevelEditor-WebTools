using UnityEngine;

[ExecutionDependency(typeof(IFlowController))]
public class Vacuumable : MonoBehaviour
{
	[SerializeField]
	private ProgressUIController m_progressUIPrefab;
}
