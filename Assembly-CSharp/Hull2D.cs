using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class Hull2D
{
	[SerializeField]
	[ReadOnly]
	public Vector2[] m_points;

	[SerializeField]
	[ReadOnly]
	public Rect m_bounds;

	public static Hull2D GenerateHull(List<Vector2> _points)
	{
		if (_points == null)
		{
			return null;
		}
		if (_points.Count < 3)
		{
			return null;
		}
		List<Vector2> list = new List<Vector2>(_points);
		list.Sort(SortPoints);
		return GenerateHullFromSorted(list);
	}

	public static Hull2D GenerateHull(Vector2[] _points)
	{
		if (_points.Length < 3)
		{
			return null;
		}
		List<Vector2> list = new List<Vector2>(_points);
		list.Sort(SortPoints);
		return GenerateHullFromSorted(list);
	}

	private static Hull2D GenerateHullFromSorted(List<Vector2> _sortedPoints)
	{
		int[] array = new int[_sortedPoints.Count * 2];
		int num = 0;
		for (int i = 0; i < _sortedPoints.Count; i++)
		{
			while (num >= 2 && TriangleArea(_sortedPoints[array[num - 2]], _sortedPoints[array[num - 1]], _sortedPoints[i]) <= 0f)
			{
				num--;
			}
			array[num++] = i;
		}
		int num2 = _sortedPoints.Count - 2;
		int num3 = num + 1;
		while (num2 >= 0)
		{
			while (num >= num3 && TriangleArea(_sortedPoints[array[num - 2]], _sortedPoints[array[num - 1]], _sortedPoints[num2]) <= 0f)
			{
				num--;
			}
			array[num++] = num2;
			num2--;
		}
		num--;
		Vector2[] array2 = new Vector2[num];
		for (int j = 0; j < num; j++)
		{
			array2[j] = _sortedPoints[array[j]];
		}
		Hull2D hull2D = new Hull2D();
		hull2D.m_points = array2;
		hull2D.m_bounds = CalculateBounds(array2);
		return hull2D;
	}

	private static Rect CalculateBounds(Vector2[] _points)
	{
		if (_points.Length == 0)
		{
			return default(Rect);
		}
		Vector2 vector = _points[0];
		float num = vector.x;
		float num2 = vector.x;
		float num3 = vector.y;
		float num4 = vector.y;
		for (int i = 1; i < _points.Length; i++)
		{
			Vector2 vector2 = _points[i];
			num = ((!(vector2.x < num)) ? num : vector2.x);
			num2 = ((!(vector2.x > num2)) ? num2 : vector2.x);
			num3 = ((!(vector2.y < num3)) ? num3 : vector2.y);
			num4 = ((!(vector2.y > num4)) ? num4 : vector2.y);
		}
		return Rect.MinMaxRect(num, num3, num2, num4);
	}

	private static int SortPoints(Vector2 _v0, Vector2 _v1)
	{
		return _v0.x.CompareTo(_v1.x);
	}

	public static float TriangleArea(Vector2 _a, Vector2 _b, Vector2 _c)
	{
		return (_b.x - _a.x) * (_c.y - _a.y) - (_c.x - _a.x) * (_b.y - _a.y);
	}

	public bool ContainsPoint(Vector2 _point)
	{
		if (m_points.Length < 3)
		{
			return false;
		}
		if (m_points.Length > 4 && !m_bounds.Contains(_point))
		{
			return false;
		}
		for (int i = 1; i < m_points.Length; i++)
		{
			if (TriangleArea(m_points[i - 1], m_points[i], _point) < 0f)
			{
				return false;
			}
		}
		if (TriangleArea(m_points[m_points.Length - 1], m_points[0], _point) < 0f)
		{
			return false;
		}
		return true;
	}
}
