using System;
using System.Collections.Generic;
using AStar;
using UnityEngine;

public class GridNavSpace : Manager
{
	private GridManager m_gridManager;

	private bool[,] m_nodeMap;

	private Point2 m_mapOffset;

	public Point2 GetNavPoint(Vector3 _position)
	{
		GridIndex gridLocationFromPos = m_gridManager.GetGridLocationFromPos(_position);
		return GetPointFromIndex(gridLocationFromPos);
	}

	private Point2 GetPointFromIndex(GridIndex _index)
	{
		return new Point2(_index.X + m_mapOffset.X, _index.Z + m_mapOffset.Y);
	}

	private GridIndex GetIndexFromPoint(Point2 _point)
	{
		return new GridIndex(_point.X - m_mapOffset.X, 0, _point.Y - m_mapOffset.Y);
	}

	public List<Vector3> FindPath(Point2 _start, Point2 _end)
	{
		SearchParameters searchParameters = new SearchParameters(_start, _end, m_nodeMap);
		PathFinder pathFinder = new PathFinder(searchParameters);
		Converter<Point2, Vector3> converter = (Point2 _input) => m_gridManager.GetPosFromGridLocation(GetIndexFromPoint(_input));
		List<Point2> list = pathFinder.FindPath();
		return list.ConvertAll(converter);
	}

	private void Awake()
	{
		m_gridManager = GameUtils.GetGridManager(base.transform);
	}

	private void Start()
	{
		Point3 gridHalfSize = m_gridManager.GetGridHalfSize();
		m_nodeMap = new bool[2 * gridHalfSize.X + 1, 2 * gridHalfSize.Z + 1];
		m_mapOffset = new Point2(gridHalfSize.X, gridHalfSize.Z);
		for (int i = 0; i < m_nodeMap.GetLength(0); i++)
		{
			for (int j = 0; j < m_nodeMap.GetLength(1); j++)
			{
				m_nodeMap[i, j] = m_gridManager.GetGridOccupant(GetIndexFromPoint(new Point2(i, j))) == null;
			}
		}
	}
}
