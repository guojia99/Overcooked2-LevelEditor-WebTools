using UnityEngine;

[RequireComponent(typeof(HeatedStation))]
public class FurnaceCosmeticDecisions : MonoBehaviour
{
	[SerializeField]
	[Range(0f, 2f)]
	public float m_heatedAnimatorMinSpeed;

	[SerializeField]
	[Range(0f, 2f)]
	public float m_heatedAnimatorMaxSpeed = 1f;

	[SerializeField]
	public Animator[] m_heatedAnimators = new Animator[0];

	[Space]
	[SerializeField]
	public GameObject m_highEffect;

	[SerializeField]
	public GameObject m_mediumEffect;

	[SerializeField]
	public GameObject m_lowEffect;

	public static readonly int s_heatedAnimatorParameterHash = Animator.StringToHash("FurnaceHeat");
}
