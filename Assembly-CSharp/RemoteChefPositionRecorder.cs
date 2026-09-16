using System.Collections.Generic;
using UnityEngine;

public class RemoteChefPositionRecorder : MonoBehaviour
{
	private class PositionData
	{
		public float Time;

		public Vector3 Position;

		public Vector3 Velocity;

		public PositionData()
		{
			Time = 0f;
			Position = default(Vector3);
			Velocity = default(Vector3);
		}
	}

	private static List<RemoteChefPositionRecorder> ms_AllRemoteChefPositionRecorders = new List<RemoteChefPositionRecorder>();

	private const int kPositionDataHistory = 100;

	private PositionData[] m_PositionHistory = new PositionData[100];

	private int m_CurrentIndex;

	private Vector3 m_RestorePosition = default(Vector3);

	private Vector3 m_RestoreVelocity = default(Vector3);

	private Rigidbody m_Rigidbody;

	public virtual void Awake()
	{
		for (int i = 0; i < 100; i++)
		{
			m_PositionHistory[i] = new PositionData();
		}
		ms_AllRemoteChefPositionRecorders.Add(this);
		m_Rigidbody = GetComponent<Rigidbody>();
	}

	public virtual void OnDestroy()
	{
		ms_AllRemoteChefPositionRecorders.Remove(this);
	}

	public static void SetRemoteChefsToTime(float _time)
	{
		for (int i = 0; i < ms_AllRemoteChefPositionRecorders.Count; i++)
		{
			ms_AllRemoteChefPositionRecorders[i].InternalSetChefToTime(_time);
		}
	}

	public static void SetChefRestorePoint()
	{
		for (int i = 0; i < ms_AllRemoteChefPositionRecorders.Count; i++)
		{
			ms_AllRemoteChefPositionRecorders[i].InternalSetChefRestorePoint();
		}
	}

	public static void RestoreChefsPositions()
	{
		for (int i = 0; i < ms_AllRemoteChefPositionRecorders.Count; i++)
		{
			ms_AllRemoteChefPositionRecorders[i].InternalRestorePosition();
		}
	}

	private void InternalSetChefRestorePoint()
	{
		m_RestorePosition = m_Rigidbody.position;
		m_RestoreVelocity = m_Rigidbody.velocity;
	}

	private void InternalRestorePosition()
	{
		m_Rigidbody.position = m_RestorePosition;
		m_Rigidbody.velocity = m_RestoreVelocity;
	}

	private void InternalSetChefToTime(float _time)
	{
		m_Rigidbody.position = GetChefPosition(_time);
	}

	public void RecordData(float _time, Vector3 _position, Vector3 _velocity)
	{
		PositionData positionData = m_PositionHistory[m_CurrentIndex++];
		if (m_CurrentIndex == 100)
		{
			m_CurrentIndex = 0;
		}
		positionData.Time = _time;
		positionData.Position = _position;
		positionData.Velocity = _velocity;
	}

	public Vector3 GetChefPosition(float _time)
	{
		float num = 0f;
		int num2 = 0;
		bool flag = false;
		for (int i = 0; i < 100; i++)
		{
			float time = m_PositionHistory[i].Time;
			if (time < _time && time > num)
			{
				num2 = i;
				num = time;
				flag = true;
			}
		}
		if (flag)
		{
			PositionData positionData = m_PositionHistory[num2];
			PositionData positionData2 = null;
			int num3 = num2 + 1;
			if (num3 == 100)
			{
				num3 = 0;
			}
			positionData2 = m_PositionHistory[num3];
			if (positionData2.Time < positionData.Time)
			{
				positionData2 = null;
				return positionData.Position + positionData.Velocity * (_time - positionData.Time);
			}
			float num4 = positionData2.Time - positionData.Time;
			float num5 = _time - positionData.Time;
			return Vector3.Lerp(positionData.Position, positionData2.Position, num5 / num4);
		}
		return m_Rigidbody.position;
	}
}
