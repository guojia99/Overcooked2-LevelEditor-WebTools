using System.Collections.Generic;
using UnityEngine;

public class LevelBounds : MonoBehaviour
{
	private static List<LevelBounds> s_bounds = new List<LevelBounds>(1);

	private const string c_floorName = "KillPlane";

	private const string c_ceilingName = "Ceiling";

	private const int c_numSides = 4;

	private const int c_numPlanes = 6;

	private Bounds m_bounds = default(Bounds);

	private void Start()
	{
		SetupLevelBounds();
		s_bounds.Add(this);
	}

	private void SetupLevelBounds()
	{
		RespawnCollider[] array = base.gameObject.RequestComponentsRecursive<RespawnCollider>();
		if (array.Length == 4)
		{
			Collider[] array2 = new Collider[6];
			for (int i = 0; i < 4; i++)
			{
				array2[i] = array[i].gameObject.RequireComponent<Collider>();
			}
			GameObject ceiling = GetCeiling();
			array2[4] = ceiling.RequireComponent<Collider>();
			RespawnCollider ground = GetGround();
			array2[5] = ground.gameObject.RequireComponent<Collider>();
			Vector3 zero = Vector3.zero;
			for (int j = 0; j < 6; j++)
			{
				zero += array2[j].bounds.center;
			}
			zero /= 6f;
			m_bounds.center = zero;
			for (int k = 0; k < 6; k++)
			{
				Vector3 point = NearestPointAABB3(array2[k].bounds.min, array2[k].bounds.max, zero);
				m_bounds.Encapsulate(point);
			}
		}
	}

	private Vector3 NearestPointAABB3(Vector3 min, Vector3 max, Vector3 p)
	{
		p.x = Mathf.Max(p.x, min.x);
		p.y = Mathf.Max(p.y, min.y);
		p.z = Mathf.Max(p.z, min.z);
		p.x = Mathf.Min(p.x, max.x);
		p.y = Mathf.Min(p.y, max.y);
		p.z = Mathf.Min(p.z, max.z);
		return p;
	}

	private RespawnCollider GetGround()
	{
		Transform transform = GameUtils.GetGameEnvironment().transform.FindChildRecursive("KillPlane");
		return transform.gameObject.RequireComponent<RespawnCollider>();
	}

	private GameObject GetCeiling()
	{
		return GameObject.Find("Ceiling");
	}

	public static bool ActiveBoundsContain(Vector3 _position)
	{
		if (s_bounds.Count > 0)
		{
			for (int i = 0; i < s_bounds.Count; i++)
			{
				LevelBounds levelBounds = s_bounds[i];
				if (levelBounds.gameObject.activeInHierarchy && levelBounds.enabled && levelBounds.Contains(_position))
				{
					return true;
				}
			}
			return false;
		}
		return true;
	}

	public bool Contains(Vector3 _position)
	{
		return m_bounds.Contains(_position);
	}

	private void OnDestroy()
	{
		s_bounds.Remove(this);
	}
}
