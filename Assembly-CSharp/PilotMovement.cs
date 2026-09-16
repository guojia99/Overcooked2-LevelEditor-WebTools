using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PilotMovement : MonoBehaviour
{
	[AssignComponent(Editorbility.NonEditable)]
	public RigidbodyMotion RigidbodyMotion;

	public float MoveSpeed = 4f;

	public float SnapHalfLife = 0.1f;

	protected Vector3 m_previousPose;

	protected Vector3 m_previousPoseDifference;

	protected Vector3 m_velocityAverage;

	protected static float m_threshold = 0.001f;

	private int m_belowThresholdCounter;

	private const int k_belowThresholdMax = 5;

	protected virtual void Start()
	{
		RigidbodyMotion.SetKinematic(true);
		m_previousPose = RigidbodyMotion.GetPosition();
	}

	protected virtual void Update()
	{
		Vector3 position = RigidbodyMotion.GetPosition();
		m_previousPoseDifference.Set(position.x - m_previousPose.x, position.y - m_previousPose.y, position.z - m_previousPose.z);
		m_previousPose = position;
		if (m_previousPoseDifference.sqrMagnitude < m_threshold)
		{
			m_belowThresholdCounter++;
		}
		else
		{
			m_belowThresholdCounter = 0;
		}
	}

	public virtual Vector3 EstimateAverageVelocity()
	{
		float deltaTime = TimeManager.GetDeltaTime(base.gameObject.layer);
		if (deltaTime > 0.001f)
		{
			Vector3 vector = m_previousPoseDifference / deltaTime;
			float num = Mathf.Min(10f * deltaTime, 1f);
			m_velocityAverage = num * vector + (1f - num) * m_velocityAverage;
		}
		return m_velocityAverage;
	}

	public virtual bool HasMoved()
	{
		return m_belowThresholdCounter < 5;
	}
}
