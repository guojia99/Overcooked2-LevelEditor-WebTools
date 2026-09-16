using UnityEngine;

public class HexGridManager : GridManager
{
	[SerializeField]
	private Vector3 m_origin;

	[SerializeField]
	private float m_hexRadius = 1f;

	[SerializeField]
	private float m_gridSizeY = 1f;

	private const float c_root3 = 1.7320508f;

	public float HexRadius
	{
		get
		{
			return m_hexRadius;
		}
	}

	public Vector3 Origin()
	{
		return m_origin;
	}

	public override Vector3 GetPosFromGridLocation(GridIndex _index)
	{
		return base.transform.TransformPoint(m_origin + new Vector3(m_hexRadius * (float)_index.X * 3f / 2f, (float)_index.Y * m_gridSizeY, m_hexRadius * ((float)_index.X * 1.7320508f / 2f + 1.7320508f * (float)_index.Z)));
	}

	protected Vector3 GetLocalPosFromGridXYZ(float x, float y, float z)
	{
		return new Vector3(m_hexRadius * x * 3f / 2f, y * m_gridSizeY, m_hexRadius * (x * 1.7320508f / 2f + 1.7320508f * z));
	}

	public override GridIndex GetUnclampedGridLocationFromPos(Vector3 _pos)
	{
		Vector3 vector = base.transform.InverseTransformPoint(_pos) - m_origin;
		float num = Mathf.Round(vector.y / m_gridSizeY);
		float f = 2f * vector.x / (3f * m_hexRadius);
		float f2 = (vector.z - vector.x / 1.7320508f) / (1.7320508f * m_hexRadius);
		float num2 = Mathf.Floor(f);
		float num3 = Mathf.Ceil(f);
		float num4 = Mathf.Floor(f2);
		float num5 = Mathf.Ceil(f2);
		Vector3 localPosFromGridXYZ = GetLocalPosFromGridXYZ(num2, num, num4);
		Vector3 localPosFromGridXYZ2 = GetLocalPosFromGridXYZ(num3, num, num4);
		Vector3 localPosFromGridXYZ3 = GetLocalPosFromGridXYZ(num2, num, num5);
		Vector3 localPosFromGridXYZ4 = GetLocalPosFromGridXYZ(num3, num, num5);
		float sqrMagnitude = (localPosFromGridXYZ - vector).sqrMagnitude;
		float sqrMagnitude2 = (localPosFromGridXYZ2 - vector).sqrMagnitude;
		float sqrMagnitude3 = (localPosFromGridXYZ3 - vector).sqrMagnitude;
		float sqrMagnitude4 = (localPosFromGridXYZ4 - vector).sqrMagnitude;
		float num6 = ((!(Mathf.Min(sqrMagnitude, sqrMagnitude3) < Mathf.Min(sqrMagnitude2, sqrMagnitude4))) ? num3 : num2);
		float num7 = ((!(Mathf.Min(sqrMagnitude, sqrMagnitude2) < Mathf.Min(sqrMagnitude3, sqrMagnitude4))) ? num5 : num4);
		return new GridIndex((int)num6, (int)num, (int)num7);
	}

	public static int ComputeDistanceHexGrid(GridIndex A, GridIndex B)
	{
		Point2 point = new Point2(0, 0);
		point.X = A.Z - B.Z;
		point.Y = -(A.X - B.X);
		Point2 point2 = new Point2(0, 0);
		int num = ((Mathf.Abs(point.X) >= Mathf.Abs(point.Y)) ? Mathf.Abs(point.Y) : Mathf.Abs(point.X));
		point2.X = ((point.X >= 0) ? num : (-num));
		point2.Y = ((point.Y >= 0) ? num : (-num));
		Point2 point3 = new Point2(0, 0);
		point3.X = point.X - point2.X;
		point3.Y = point.Y - point2.Y;
		int num2 = Mathf.Abs(point3.X) + Mathf.Abs(point3.Y);
		int num3 = Mathf.Abs(point2.X);
		if ((point2.X < 0 && point2.Y > 0) || (point2.X > 0 && point2.Y < 0))
		{
			num3 *= 2;
		}
		return num2 + num3;
	}
}
