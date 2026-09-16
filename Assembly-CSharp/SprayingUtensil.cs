using UnityEngine;

public class SprayingUtensil : MonoBehaviour
{
	[SerializeField]
	public string m_startSprayTrigger;

	[SerializeField]
	public string m_stopSprayTrigger;

	[SerializeField]
	public GameObject m_sprayEffectPrefab;

	[SerializeField]
	public Transform m_effectAttachPoint;

	[SerializeField]
	public float m_sprayAngleInDegrees = 15f;

	[SerializeField]
	public float m_sprayDistance = 4f;

	[SerializeField]
	public GameLoopingAudioTag m_audioTag = GameLoopingAudioTag.ExtinguisherSpray;

	[SerializeField]
	public LayerMask m_CollisionLayerMask = -1;

	public const float c_itemRadius = 0.6f;
}
