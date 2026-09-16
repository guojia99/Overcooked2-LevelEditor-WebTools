using UnityEngine;

public class PilotRotation : PilotMovement
{
	public Transform m_transformToRotate;

	[Range(-45f, 45f)]
	public float m_minLimitDegrees = -45f;

	[Range(-45f, 45f)]
	public float m_maxLimitDegrees = 45f;

	public string m_sessionBegunTrigger;

	public string m_sessionEndedTrigger;

	private bool m_bEstimateVelocityInX;

	private int m_directionModifier;

	protected override void Start()
	{
		m_previousPose = m_transformToRotate.rotation.eulerAngles;
		float num = 0.1f;
		Vector3 right = m_transformToRotate.right;
		Vector3 right2 = Vector3.right;
		float num2 = Vector3.Dot(right, right2);
		m_bEstimateVelocityInX = Mathf.Abs(num2) > num;
		m_directionModifier = ((num2 > 0f) ? 1 : (-1));
	}

	protected override void Update()
	{
		Vector3 eulerAngles = m_transformToRotate.rotation.eulerAngles;
		m_previousPoseDifference.Set(Mathf.DeltaAngle(m_previousPose.x, eulerAngles.x), Mathf.DeltaAngle(m_previousPose.y, eulerAngles.y), Mathf.DeltaAngle(m_previousPose.z, eulerAngles.z));
		if (m_previousPoseDifference.sqrMagnitude > PilotMovement.m_threshold)
		{
			m_previousPose = m_transformToRotate.rotation.eulerAngles;
		}
	}

	public override Vector3 EstimateAverageVelocity()
	{
		Vector3 result = base.EstimateAverageVelocity();
		if (m_bEstimateVelocityInX)
		{
			result.Set(result.y * (float)m_directionModifier, 0f, 0f);
		}
		else
		{
			result.Set(0f, 0f, result.y * (float)m_directionModifier);
		}
		return result;
	}

	public override bool HasMoved()
	{
		return m_previousPoseDifference.sqrMagnitude > PilotMovement.m_threshold;
	}
}
