using System.Collections.Generic;

public struct GridIndex : IEqualityComparer<GridIndex>
{
	private int m_x;

	private int m_y;

	private int m_z;

	public int X
	{
		get
		{
			return m_x;
		}
	}

	public int Y
	{
		get
		{
			return m_y;
		}
	}

	public int Z
	{
		get
		{
			return m_z;
		}
	}

	public GridIndex(int _x, int _y, int _z)
	{
		m_x = _x;
		m_y = _y;
		m_z = _z;
	}

	public static bool operator ==(GridIndex _token1, GridIndex _token2)
	{
		return _token1.X == _token2.X && _token1.Y == _token2.Y && _token1.Z == _token2.Z;
	}

	public static bool operator !=(GridIndex _token1, GridIndex _token2)
	{
		return !(_token1 == _token2);
	}

	public static GridIndex operator +(GridIndex _token1, GridIndex _token2)
	{
		return new GridIndex(_token1.X + _token2.X, _token1.Y + _token2.Y, _token1.Z + _token2.Z);
	}

	public override bool Equals(object obj)
	{
		if (obj.GetType() == GetType())
		{
			GridIndex gridIndex = (GridIndex)obj;
			return X == gridIndex.X && Y == Y && Z == gridIndex.Z;
		}
		return false;
	}

	public override int GetHashCode()
	{
		return (m_x << 16) ^ (m_y << 8) ^ m_z;
	}

	public bool Equals(GridIndex x, GridIndex y)
	{
		return x.GetHashCode() == y.GetHashCode();
	}

	public int GetHashCode(GridIndex obj)
	{
		return (obj.m_x << 16) ^ (obj.m_y << 8) ^ m_z;
	}

	public override string ToString()
	{
		return "(" + m_x + ", " + m_y + ", " + m_z + ")";
	}
}
