using UnityEngine;

public class CookingEffectsCosmeticDecisions : MonoBehaviour
{
	[SerializeField]
	public ParticleSystem m_steamEffect;

	public Animator m_animator;

	public static int c_onParam = Animator.StringToHash("On");

	public static int c_cookingParam = Animator.StringToHash("Cooking");

	[HideInInspector]
	public bool m_hasOnParam;

	[HideInInspector]
	public bool m_hasCookingParam;

	private void Awake()
	{
		m_animator = GetComponentInChildren<Animator>();
		SetEmissionRate(0f);
		if (m_animator != null)
		{
			m_hasOnParam = m_animator.HasParameter(c_onParam);
			m_hasCookingParam = m_animator.HasParameter(c_cookingParam);
		}
	}

	public void SetEmissionRate(float _rate)
	{
		ParticleSystem.EmissionModule emission = m_steamEffect.emission;
		emission.rateOverTime = _rate;
	}

	public void SetColor(Color _color)
	{
		ParticleSystem.MainModule main = m_steamEffect.main;
		main.startColor = _color;
	}
}
