using UnityEngine;

public class QuadGridManager : GridManager
{
	[SerializeField]
	private Vector3 m_origin;

	[SerializeField]
	private Vector3 m_size = Vector3.one;

	public Vector3 AccessOrigin
	{
		get
		{
			return m_origin;
		}
		set
		{
			m_origin = value;
		}
	}

	public Vector3 Origin()
	{
		return m_origin;
	}

	public override Vector3 GetPosFromGridLocation(GridIndex _index)
	{
		return base.transform.TransformPoint(m_origin + new Vector3((float)_index.X * m_size.x, (float)_index.Y * m_size.y, (float)_index.Z * m_size.z));
	}

	public override GridIndex GetUnclampedGridLocationFromPos(Vector3 _pos)
	{
		Vector3 vector = base.transform.InverseTransformPoint(_pos) - m_origin;
		Vector3 vector2 = new Vector3(Mathf.Round(vector.x / m_size.x), Mathf.Round(vector.y / m_size.y), Mathf.Round(vector.z / m_size.z));
		return new GridIndex((int)vector2.x, (int)vector2.y, (int)vector2.z);
	}
}
