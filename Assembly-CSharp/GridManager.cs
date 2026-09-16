using System.Collections.Generic;
using UnityEngine;

public abstract class GridManager : Manager
{
	[SerializeField]
	private Point3 m_gridHalfSize = new Point3(20, 1, 20);

	private Dictionary<GridIndex, GameObject> m_gridOccupancy = new Dictionary<GridIndex, GameObject>(default(GridIndex));

	private static List<GridManager> s_activeGrids = new List<GridManager>();

	public Point3 AccessGridHalfSize
	{
		get
		{
			return m_gridHalfSize;
		}
		set
		{
			m_gridHalfSize = value;
		}
	}

	private void OnEnable()
	{
		s_activeGrids.Add(this);
	}

	private void OnDisable()
	{
		s_activeGrids.RemoveAll(Equals);
	}

	public static int GetActiveCount()
	{
		return s_activeGrids.Count;
	}

	public static GridManager GetActive(int _index)
	{
		return s_activeGrids[_index];
	}

	public abstract Vector3 GetPosFromGridLocation(GridIndex _index);

	public abstract GridIndex GetUnclampedGridLocationFromPos(Vector3 _pos);

	public GridIndex GetGridLocationFromPos(Vector3 _pos)
	{
		GridIndex unclampedGridLocationFromPos = GetUnclampedGridLocationFromPos(_pos);
		Point3 gridHalfSize = GetGridHalfSize();
		int x = Mathf.Clamp(unclampedGridLocationFromPos.X, -gridHalfSize.X, gridHalfSize.X);
		int y = Mathf.Clamp(unclampedGridLocationFromPos.Y, -gridHalfSize.Y, gridHalfSize.Y);
		int z = Mathf.Clamp(unclampedGridLocationFromPos.Z, -gridHalfSize.Z, gridHalfSize.Z);
		return new GridIndex(x, y, z);
	}

	public Point3 GetGridHalfSize()
	{
		return m_gridHalfSize;
	}

	public void OccupyGrid(GameObject _object, GridIndex _index)
	{
		if (m_gridOccupancy.ContainsKey(_index))
		{
			GameObject obj = m_gridOccupancy[_index];
			IHandleGridTransfer handleGridTransfer = obj.RequestInterface<IHandleGridTransfer>();
			if (handleGridTransfer != null && handleGridTransfer.CanHandleTransfer(_index, _object))
			{
				handleGridTransfer.HandleTransfer(_index, _object);
				m_gridOccupancy[_index] = _object;
			}
		}
		else
		{
			m_gridOccupancy.Add(_index, _object);
		}
	}

	public GameObject GetGridOccupant(GridIndex _index)
	{
		GameObject value = null;
		m_gridOccupancy.TryGetValue(_index, out value);
		return value;
	}

	public void DeoccupyGrid(GridIndex _index)
	{
		m_gridOccupancy.Remove(_index);
	}

	public bool TryOccupyGridRegion(GridIndex min, GridIndex max, GameObject go)
	{
		for (int i = min.Z; i <= max.Z; i++)
		{
			for (int j = min.Y; j <= max.Y; j++)
			{
				for (int k = min.X; k <= max.X; k++)
				{
					GridIndex gridIndex = new GridIndex(k, j, i);
					if (m_gridOccupancy.ContainsKey(gridIndex))
					{
						GameObject gridOccupant = GetGridOccupant(gridIndex);
						return false;
					}
				}
			}
		}
		for (int l = min.Z; l <= max.Z; l++)
		{
			for (int m = min.Y; m <= max.Y; m++)
			{
				for (int n = min.X; n <= max.X; n++)
				{
					GridIndex index = new GridIndex(n, m, l);
					OccupyGrid(go, index);
				}
			}
		}
		return true;
	}

	public void DeoccupyGridRegion(GridIndex min, GridIndex max)
	{
		for (int i = min.Z; i <= max.Z; i++)
		{
			for (int j = min.Y; j <= max.Y; j++)
			{
				for (int k = min.X; k <= max.X; k++)
				{
					GridIndex key = new GridIndex(k, j, i);
					m_gridOccupancy.Remove(key);
				}
			}
		}
	}
}
