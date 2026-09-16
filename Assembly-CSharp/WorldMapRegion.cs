using System.Collections.Generic;
using UnityEngine;

public class WorldMapRegion : MonoBehaviour
{
	[SerializeField]
	private int m_priority;

	[SerializeField]
	public Hull2D m_hull;

	[SerializeField]
	public WorldMapRegionData m_data;

	private static List<WorldMapRegion> s_allRegions;

	[SerializeField]
	public int Priority
	{
		get
		{
			return m_priority;
		}
	}

	private void Awake()
	{
		if (s_allRegions == null)
		{
			s_allRegions = new List<WorldMapRegion>(1);
		}
		s_allRegions.Add(this);
		s_allRegions.Sort((WorldMapRegion x, WorldMapRegion y) => y.m_priority - x.m_priority);
	}

	private void OnDestroy()
	{
		s_allRegions.Remove(this);
		if (s_allRegions.Count == 0)
		{
			s_allRegions = null;
		}
	}

	public static WorldMapRegion FindRegionForPoint(Vector3 _point)
	{
		return FindRegionForPoint(new Vector2(_point.x, _point.z));
	}

	public static WorldMapRegion FindRegionForPoint(Vector2 _point)
	{
		if (s_allRegions == null)
		{
			return null;
		}
		for (int i = 0; i < s_allRegions.Count; i++)
		{
			WorldMapRegion worldMapRegion = s_allRegions[i];
			if (worldMapRegion.ContainsPoint(_point))
			{
				return worldMapRegion;
			}
		}
		return null;
	}

	public bool ContainsPoint(Vector3 _point)
	{
		if (m_hull == null)
		{
			return false;
		}
		return m_hull.ContainsPoint(new Vector2(_point.x, _point.z));
	}

	public bool ContainsPoint(Vector2 _point)
	{
		if (m_hull == null)
		{
			return false;
		}
		return m_hull.ContainsPoint(_point);
	}
}
