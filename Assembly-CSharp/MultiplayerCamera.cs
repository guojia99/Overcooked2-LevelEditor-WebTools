using System;
using UnityEngine;
using UnityEngine.Serialization;

[ExecutionDependency(typeof(PlayerControls))]
public class MultiplayerCamera : MonoBehaviour
{
	private Transform[] m_avatars;

	[SerializeField]
	private float m_gradientLimit = 0.5f;

	[SerializeField]
	private float m_timeToMax = 0.5f;

	[SerializeField]
	[FormerlySerializedAs("m_xEdgeBuffer")]
	[Range(0f, 0.5f)]
	private float m_leftEdgeBuffer = 0.2f;

	[SerializeField]
	[FormerlySerializedAs("m_xEdgeBuffer")]
	[Range(0f, 0.5f)]
	private float m_rightEdgeBuffer = 0.2f;

	[SerializeField]
	[FormerlySerializedAs("m_yEdgeBuffer")]
	[Range(0f, 0.5f)]
	private float m_topEdgeBuffer = 0.2f;

	[SerializeField]
	[FormerlySerializedAs("m_yEdgeBuffer")]
	[Range(0f, 0.5f)]
	private float m_bottomEdgeBuffer = 0.2f;

	[SerializeField]
	private float m_minDistance = 5f;

	[SerializeField]
	private float m_maxDistance = 50f;

	[SerializeField]
	private float m_maxUDistance = 2f;

	[SerializeField]
	private float m_maxRDistance = 2f;

	private Vector3 m_basePos;

	private float m_currentGradient;

	private Camera m_camera;

	private bool m_bStarted;

	private void Awake()
	{
		m_camera = base.gameObject.RequireComponentRecursive<Camera>();
		PlayerIDProvider.OnPlayerIDProviderDestroyed = (GenericVoid)Delegate.Combine(PlayerIDProvider.OnPlayerIDProviderDestroyed, new GenericVoid(OnPlayerIDProviderDestroyed));
		SetupAvatars();
		m_basePos = base.transform.position + base.transform.forward * GetAverageDistance();
		base.transform.position = GetIdealLocation();
		m_bStarted = true;
	}

	private void OnDestroy()
	{
		PlayerIDProvider.OnPlayerIDProviderDestroyed = (GenericVoid)Delegate.Remove(PlayerIDProvider.OnPlayerIDProviderDestroyed, new GenericVoid(OnPlayerIDProviderDestroyed));
	}

	private void OnPlayerIDProviderDestroyed()
	{
		if (m_bStarted)
		{
			SetupAvatars();
		}
	}

	private void SetupAvatars()
	{
		int count = PlayerIDProvider.s_AllProviders.Count;
		m_avatars = new Transform[count];
		for (int i = 0; i < count; i++)
		{
			m_avatars[i] = PlayerIDProvider.s_AllProviders._items[i].transform;
		}
	}

	private float GetAverageDistance()
	{
		Camera camera = m_camera;
		Vector3 position = base.transform.position;
		Vector3 forward = base.transform.forward;
		float num = 0f;
		if (m_avatars != null)
		{
			for (int i = 0; i < m_avatars.Length; i++)
			{
				Vector3 lhs = m_avatars[i].position - position;
				num += Vector3.Dot(lhs, forward);
			}
			return num / (float)m_avatars.Length;
		}
		return 0f;
	}

	private Vector3 GetCentralizedCameraPosition()
	{
		if (m_avatars != null)
		{
			Camera camera = m_camera;
			Vector3 up = base.transform.up;
			float num = m_avatars.Length;
			float num2 = 0f;
			for (int i = 0; i < m_avatars.Length; i++)
			{
				num2 += Vector3.Dot(m_avatars[i].position - m_basePos, up);
			}
			float value = 1f / num * num2;
			Vector3 right = base.transform.right;
			float num3 = 0f;
			for (int j = 0; j < m_avatars.Length; j++)
			{
				num3 += Vector3.Dot(m_avatars[j].position - m_basePos, right);
			}
			float value2 = 1f / num * num3;
			value = Mathf.Clamp(value, 0f - m_maxUDistance, m_maxUDistance);
			value2 = Mathf.Clamp(value2, 0f - m_maxRDistance, m_maxRDistance);
			return m_basePos + up * value + right * value2;
		}
		return Vector3.zero;
	}

	private float GetIdealDistance(Vector3 _centralisedCamera)
	{
		if (m_avatars != null)
		{
			Camera camera = m_camera;
			float fieldOfView = camera.fieldOfView;
			float aspect = camera.aspect;
			Vector3 right = base.transform.right;
			Vector3 up = base.transform.up;
			Vector3 forward = base.transform.forward;
			float num = 0f;
			float num2 = Mathf.Tan(0.5f * fieldOfView);
			for (int i = 0; i < m_avatars.Length; i++)
			{
				Vector3 position = m_avatars[i].position;
				Vector3 lhs = position - _centralisedCamera;
				float num3 = Vector3.Dot(lhs, right) / (2f * aspect * num2 * (0.5f - m_rightEdgeBuffer));
				float num4 = (0f - Vector3.Dot(lhs, right)) / (2f * aspect * num2 * (0.5f - m_leftEdgeBuffer));
				float num5 = Vector3.Dot(lhs, up) / (2f * num2 * (0.5f - m_topEdgeBuffer));
				float num6 = (0f - Vector3.Dot(lhs, up)) / (2f * num2 * (0.5f - m_bottomEdgeBuffer));
				float num7 = Vector3.Dot(m_basePos - position, forward);
				num3 += num7;
				num4 += num7;
				num5 += num7;
				num6 += num7;
				num = Mathf.Max(num3, num);
				num = Mathf.Max(num4, num);
				num = Mathf.Max(num5, num);
				num = Mathf.Max(num6, num);
			}
			return num;
		}
		return 0f;
	}

	private Vector3 GetIdealLocation()
	{
		Vector3 centralizedCameraPosition = GetCentralizedCameraPosition();
		float num = Mathf.Clamp(GetIdealDistance(centralizedCameraPosition), m_minDistance, m_maxDistance);
		return centralizedCameraPosition - num * base.transform.forward;
	}

	private void FixedUpdate()
	{
		if (m_bStarted)
		{
			Vector3 idealLocation = GetIdealLocation();
			float _nCurrentX = (idealLocation - base.transform.position).magnitude;
			float fixedDeltaTime = TimeManager.GetFixedDeltaTime(base.gameObject);
			MathUtils.AdvanceToTarget_Sinusoidal(ref _nCurrentX, ref m_currentGradient, 0f, m_gradientLimit, m_timeToMax, fixedDeltaTime);
			base.transform.position = idealLocation - (idealLocation - base.transform.position).SafeNormalised(Vector3.zero) * _nCurrentX;
		}
	}
}
