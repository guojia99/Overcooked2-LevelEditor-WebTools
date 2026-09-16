using UnityEngine;

public class WokEffectsCosmeticDecisions : MonoBehaviour
{
	[SerializeField]
	[AssignChildRecursive("wok_flame", Editorbility.Editable)]
	public Renderer m_flameRenderer;

	[SerializeField]
	[AssignComponentRecursive(Editorbility.Editable)]
	public ParticleSystem[] m_particleSystems;

	[SerializeField]
	public AnimationCurve m_emissionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

	[SerializeField]
	public float m_transitionDuration = 1f;
}
