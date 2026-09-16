using UnityEngine;

public class FollowCamera : MonoBehaviour
{
	public GameObject Target;

	public Vector3 IdealOffset;

	public float GradientLimit = 0.5f;

	public float TimeToMax = 0.5f;

	public OptionalBounds TargetBounds = new OptionalBounds();

	private float m_currentGradient;

	private GameObject m_previousTarget;

	private Rigidbody m_TargetRigidBody;

	public Vector3 GetIdealLocation()
	{
		Vector3 vector = Target.transform.position;
		if (TargetBounds.HasValue)
		{
			Bounds value = TargetBounds.Value;
			vector = vector.Clamp(value.min, value.max);
		}
		return vector + IdealOffset;
	}

	private void Update()
	{
		if (m_previousTarget != Target)
		{
			m_previousTarget = Target;
			m_TargetRigidBody = Target.RequestComponent<Rigidbody>();
		}
		Vector3 idealLocation = GetIdealLocation();
		float num = GradientLimit;
		if (m_TargetRigidBody != null)
		{
			num = Mathf.Max(num, m_TargetRigidBody.velocity.magnitude);
		}
		float _nCurrentX = (idealLocation - base.transform.position).magnitude;
		float deltaTime = TimeManager.GetDeltaTime(base.gameObject);
		MathUtils.AdvanceToTarget_Sinusoidal(ref _nCurrentX, ref m_currentGradient, 0f, num, TimeToMax, deltaTime);
		base.transform.position = idealLocation - (idealLocation - base.transform.position).SafeNormalised(Vector3.zero) * _nCurrentX;
	}
}
