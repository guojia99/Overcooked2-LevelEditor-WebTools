using UnityEngine;

public class FollowPilotMoved : MonoBehaviour
{
	public PilotMovement Target;

	private Vector3 m_localPosition;

	private void Awake()
	{
		m_localPosition = Target.transform.InverseTransformPoint(base.transform.position);
	}

	private void LateUpdate()
	{
		base.transform.position = Target.transform.TransformPoint(m_localPosition);
	}
}
