using UnityEngine;

[RequireComponent(typeof(Collider))]
public class BalloonAnimateOnHit : MonoBehaviour
{
	private AudioManager m_AudioManager;

	[SerializeField]
	public GameObject m_popParticles;

	[SerializeField]
	public string m_trigger = string.Empty;

	[SerializeField]
	public Animator m_targetAnimator;

	private int m_triggerHash;

	protected virtual void Awake()
	{
		m_triggerHash = Animator.StringToHash(m_trigger);
		m_AudioManager = GameUtils.RequireManager<AudioManager>();
	}

	private void OnTriggerEnter(Collider other)
	{
		if (m_targetAnimator != null)
		{
			m_targetAnimator.SetTrigger(m_triggerHash);
		}
		if (m_popParticles != null)
		{
			m_popParticles.SetActive(true);
		}
	}

	public void OnTrigger(string _trigger)
	{
		if (_trigger == "animationFinished")
		{
			if (m_popParticles != null)
			{
				m_popParticles.SetActive(false);
			}
			m_AudioManager.TriggerAudio(GameOneShotAudioTag.UIPop, base.gameObject.layer);
			base.gameObject.SetActive(false);
		}
	}
}
