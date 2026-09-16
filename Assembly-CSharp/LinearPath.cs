using System.Collections.Generic;
using UnityEngine;

public class LinearPath : MonoBehaviour, ISerializationCallbackReceiver
{
	[SerializeField]
	private List<Vector3> m_points = new List<Vector3>();

	[SerializeField]
	private float m_totalDistance;

	[SerializeField]
	private float[] m_distances = new float[0];

	public List<Vector3> Points
	{
		get
		{
			return m_points;
		}
	}

	public float[] Distances
	{
		get
		{
			return m_distances;
		}
	}

	public float TotalDistance
	{
		get
		{
			return m_totalDistance;
		}
	}

	public Vector3 Evaluate(float t)
	{
		if (m_points.Count == 1)
		{
			return m_points[0];
		}
		float[] distances = m_distances;
		float num = t * m_totalDistance;
		float num2 = 0f;
		int num3 = -1;
		for (int i = 0; i < distances.Length; i++)
		{
			float num4 = distances[i];
			if (num2 + num4 > num)
			{
				num3 = i;
				break;
			}
			num2 += num4;
		}
		if (num3 == -1 || num3 + 1 > m_points.Count - 1)
		{
			return m_points[m_points.Count - 1];
		}
		num3 = Mathf.Clamp(num3, 0, m_points.Count - 1);
		float num5 = num - num2;
		float t2 = num5 / distances[num3];
		return Vector3.Lerp(m_points[num3], m_points[num3 + 1], t2);
	}

	public void OnAfterDeserialize()
	{
	}

	public void OnBeforeSerialize()
	{
		m_distances = new float[m_points.Count];
		float num = 0f;
		for (int i = 1; i < m_points.Count; i++)
		{
			float num2 = Vector3.Distance(m_points[i - 1], m_points[i]);
			num += num2;
			m_distances[i - 1] = num2;
		}
		m_totalDistance = num;
	}
}
