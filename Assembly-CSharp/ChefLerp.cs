using UnityEngine;

public class ChefLerp : MonoBehaviour
{
	private Transform m_Transform;

	private Rigidbody m_Rigidbody;

	private Vector3 m_LerpStart = Vector3.zero;

	private float m_LerpTimer;

	private const float kLerpLength = 0.3f;

	public void Setup(Rigidbody rigidbody)
	{
		m_Rigidbody = rigidbody;
		m_Transform = base.transform;
	}

	public virtual void Update()
	{
		if (m_LerpTimer > 0f)
		{
			float t = m_LerpTimer / 0.3f;
			m_Transform.position = Vector3.Lerp(m_Rigidbody.position, m_LerpStart, t);
			m_LerpTimer -= TimeManager.GetDeltaTime(base.gameObject);
			if (m_LerpTimer <= 0f)
			{
				m_Transform.localPosition = Vector3.zero;
			}
		}
	}

	public void StartLerp(Vector3 lerpStart)
	{
		m_Transform.position = lerpStart;
		m_LerpStart = lerpStart;
		m_LerpTimer = 0.3f;
	}
}
