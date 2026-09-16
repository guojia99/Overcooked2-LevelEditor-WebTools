using UnityEngine;

[ExecutionDependency(typeof(IFlowController))]
public class Flammable : MonoBehaviour
{
	[SerializeField]
	public GameObject m_fireEffectPrefab;

	[SerializeField]
	public ProgressUIController m_progressUIPrefab;

	[SerializeField]
	public float m_fireSpreadRadius = 1.5f;

	[SerializeField]
	public float m_overrideTargetFlammabilityTime;

	[SerializeField]
	public bool m_overrideTargetFlammability;

	[SerializeField]
	public bool m_startOnFire;

	[SerializeField]
	public GameLoopingAudioTag m_audioTag = GameLoopingAudioTag.Flames;
}
