using UnityEngine;

[RequireComponent(typeof(Collider))]
public class WalkingBread : MonoBehaviour
{
	public enum FallRotation
	{
		Towards = 0,
		Away = 1,
		None = 2
	}

	[SerializeField]
	public string m_trigger = string.Empty;

	[SerializeField]
	public Animator m_targetAnimator;

	private int m_triggerHash;

	[SerializeField]
	public FallRotation m_fallRotation;

	protected virtual void Awake()
	{
		m_triggerHash = Animator.StringToHash(m_trigger);
	}

	private void OnTriggerEnter(Collider other)
	{
		if (m_targetAnimator != null)
		{
			m_targetAnimator.SetTrigger(m_triggerHash);
		}
		if (m_fallRotation != FallRotation.None)
		{
			Vector3 toDirection = Vector3.Normalize((other.transform.position - base.transform.position).WithY(0f));
			if (m_fallRotation == FallRotation.Towards)
			{
				toDirection *= -1f;
			}
			Quaternion quaternion = Quaternion.FromToRotation(base.transform.forward, toDirection);
			base.transform.rotation *= quaternion;
		}
	}
}
