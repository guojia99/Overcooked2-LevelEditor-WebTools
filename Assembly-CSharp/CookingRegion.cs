using UnityEngine;

[RequireComponent(typeof(TriggerRecorder))]
public class CookingRegion : MonoBehaviour
{
	[SerializeField]
	[AssignChildRecursive("glow", Editorbility.Editable)]
	public ParticleSystem m_glowEffect;

	[SerializeField]
	public float m_flameOffRadius = 2f;

	[SerializeField]
	public AnimationCurve m_heightCurve;

	[SerializeField]
	public float m_fadeDuration = 1f;

	[SerializeField]
	public Renderer m_burnerRenderer;

	[SerializeField]
	[AssignComponentRecursive(Editorbility.Editable)]
	public ParticleSystem[] m_flameEffects;

	public Collider m_TriggerArea;

	public CookingStationType m_StationType;

	private void OnEnable()
	{
	}
}
