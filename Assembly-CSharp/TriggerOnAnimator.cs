using UnityEngine;

public class TriggerOnAnimator : MonoBehaviour
{
	[SerializeField]
	public string m_triggerToReceive;

	[SerializeField]
	public string m_triggerToFire;

	[SerializeField]
	public Animator m_targetAnimator;

	public int m_triggerToFireHash;

	protected virtual void Awake()
	{
		m_triggerToFireHash = Animator.StringToHash(m_triggerToFire);
	}
}
