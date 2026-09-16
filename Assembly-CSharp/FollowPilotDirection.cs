using UnityEngine;

public class FollowPilotDirection : MonoBehaviour
{
	[SerializeField]
	private PilotMovement m_target;

	[SerializeField]
	private Transform[] m_followingTransforms;

	private float m_threshold = 0.001f;

	private Vector3 m_lastPosition = Vector3.zero;

	private Quaternion[] m_defaultRotations;

	private void Start()
	{
		m_defaultRotations = new Quaternion[m_followingTransforms.Length];
		for (int i = 0; i < m_defaultRotations.Length; i++)
		{
			m_defaultRotations[i] = m_followingTransforms[i].rotation;
		}
	}

	private void Update()
	{
		Vector3 forward = m_target.transform.position - m_lastPosition;
		bool flag = forward.sqrMagnitude > m_threshold;
		for (int i = 0; i < m_followingTransforms.Length; i++)
		{
			if (flag)
			{
				m_followingTransforms[i].rotation = Quaternion.RotateTowards(m_followingTransforms[i].rotation, Quaternion.LookRotation(forward, Vector3.up), 10f);
			}
			else
			{
				m_followingTransforms[i].rotation = Quaternion.RotateTowards(m_followingTransforms[i].rotation, m_defaultRotations[i], 10f);
			}
		}
		m_lastPosition = m_target.transform.position;
	}
}
