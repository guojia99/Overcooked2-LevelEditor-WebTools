using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MapAvatarControls))]
[RequireComponent(typeof(MapAvatarGroundCast))]
public class MapAvatarTrail : MonoBehaviour
{
	[Serializable]
	protected class TrailConfig
	{
		[SerializeField]
		public GameObject m_prefab;

		[SerializeField]
		public Transform m_transform;

		[SerializeField]
		public Vector3 m_offset = new Vector3(0f, 0f, 0f);

		[SerializeField]
		public bool m_rotateWithThis;

		[SerializeField]
		[Tooltip("String used to match against a tile's cosmetics name")]
		public string[] m_trailableTiles = new string[1] { "Snow" };
	}

	protected class Trail
	{
		public struct PointInfo
		{
			public Vector3 m_point;

			public bool m_trailableTerrain;
		}

		public TrailConfig m_config;

		public PointInfo? m_lastPoint;

		public GameObject m_lastHit;

		public Transform m_trailTransform;

		public TrailRenderer[] m_currRenderers;

		public void Reset()
		{
			m_lastPoint = null;
			m_lastHit = null;
			m_trailTransform = null;
			m_currRenderers = null;
		}
	}

	[Header("Shared settings")]
	[SerializeField]
	private LayerMask m_landscapeMask = default(LayerMask);

	[Header("Per instance settings")]
	[SerializeField]
	private TrailConfig[] m_configs = new TrailConfig[0];

	private MapAvatarControls m_controls;

	private MapAvatarGroundCast m_groundCast;

	private Trail[] m_trails;

	private List<IEnumerator> m_killRoutines = new List<IEnumerator>();

	private float m_sqrtThree = Mathf.Sqrt(3f);

	protected virtual void Start()
	{
		m_controls = base.gameObject.RequireComponent<MapAvatarControls>();
		m_groundCast = base.gameObject.RequireComponent<MapAvatarGroundCast>();
		m_trails = new Trail[m_configs.Length];
		for (int i = 0; i < m_trails.Length; i++)
		{
			m_trails[i] = new Trail();
			m_trails[i].m_config = m_configs[i];
		}
	}

	protected virtual void Update()
	{
		for (int i = 0; i < m_trails.Length; i++)
		{
			UpdateTrail(ref m_trails[i]);
		}
		for (int num = m_killRoutines.Count - 1; num >= 0; num--)
		{
			IEnumerator enumerator = m_killRoutines[num];
			if (enumerator == null || !enumerator.MoveNext())
			{
				m_killRoutines.RemoveAt(num);
			}
		}
	}

	private void UpdateTrail(ref Trail _trail)
	{
		GameObject gameObject = null;
		Vector3 vector = new Vector3(0f, 0f, 0f);
		if (m_groundCast != null)
		{
			Vector3 closestPointOnGround = m_groundCast.GetClosestPointOnGround();
			Vector3 groundNormal = m_groundCast.GetGroundNormal();
			Vector3 point = _trail.m_config.m_transform.position + base.transform.rotation * _trail.m_config.m_offset;
			vector = ProjectOntoPlane(point, closestPointOnGround, groundNormal);
			gameObject = GetGridOccupantAtPoint(vector);
		}
		if (gameObject != null)
		{
			if (_trail.m_lastHit != gameObject)
			{
				ProcessGroundChange(gameObject, vector, ref _trail);
			}
			else if (_trail.m_trailTransform != null)
			{
				if (_trail.m_config.m_rotateWithThis)
				{
					_trail.m_trailTransform.rotation = base.transform.rotation;
				}
				_trail.m_trailTransform.position = vector + base.transform.rotation * _trail.m_config.m_offset;
			}
			_trail.m_lastHit = gameObject;
		}
		else if (_trail.m_lastHit != null)
		{
			_trail.m_lastHit = null;
			Trail.PointInfo newPointInfo = new Trail.PointInfo
			{
				m_trailableTerrain = false
			};
			Vector3 vector2 = _trail.m_config.m_transform.position + base.transform.rotation * _trail.m_config.m_offset;
			Vector3 _transitionPoint;
			if (FindTransitionPoint(_trail.m_lastPoint.Value.m_point, vector2, out _transitionPoint))
			{
				newPointInfo.m_point = _transitionPoint;
			}
			else
			{
				newPointInfo.m_point = vector2;
			}
			UpdateRenderer(newPointInfo, ref _trail);
		}
	}

	private void ProcessGroundChange(GameObject _ground, Vector3 _groundPoint, ref Trail _trail)
	{
		Trail.PointInfo newPointInfo = new Trail.PointInfo
		{
			m_trailableTerrain = IsTrailableTerrain(_groundPoint, _trail.m_config)
		};
		if (_trail.m_lastPoint.HasValue && newPointInfo.m_trailableTerrain != _trail.m_lastPoint.Value.m_trailableTerrain)
		{
			Vector3 _transitionPoint;
			if (FindTransitionPoint(_trail.m_lastPoint.Value.m_point, _groundPoint, out _transitionPoint))
			{
				newPointInfo.m_point = _transitionPoint;
			}
			else
			{
				newPointInfo.m_point = _groundPoint;
			}
		}
		else
		{
			newPointInfo.m_point = _groundPoint;
		}
		UpdateRenderer(newPointInfo, ref _trail);
	}

	private void UpdateRenderer(Trail.PointInfo _newPointInfo, ref Trail _trail)
	{
		if (!_newPointInfo.m_trailableTerrain && _trail.m_trailTransform != null)
		{
			m_killRoutines.Add(KillTrailRoutine(_trail.m_trailTransform, _trail.m_currRenderers, _newPointInfo.m_point));
			OnTransition(_trail, _newPointInfo);
			_trail.Reset();
		}
		if (_newPointInfo.m_trailableTerrain && (_trail.m_trailTransform == null || !_trail.m_lastPoint.HasValue || !_trail.m_lastPoint.Value.m_trailableTerrain))
		{
			GameObject gameObject = UnityEngine.Object.Instantiate(_trail.m_config.m_prefab);
			TrailRenderer[] array = gameObject.RequestComponentsRecursive<TrailRenderer>();
			foreach (TrailRenderer trailRenderer in array)
			{
				trailRenderer.Clear();
				trailRenderer.autodestruct = false;
			}
			_trail.m_currRenderers = array;
			_trail.m_trailTransform = gameObject.transform;
		}
		if (_trail.m_trailTransform != null)
		{
			if (_trail.m_config.m_rotateWithThis)
			{
				_trail.m_trailTransform.rotation = base.transform.rotation;
			}
			_trail.m_trailTransform.position = _newPointInfo.m_point + base.transform.rotation * _trail.m_config.m_offset;
		}
		_trail.m_lastPoint = _newPointInfo;
	}

	private IEnumerator KillTrailRoutine(Transform _trail, TrailRenderer[] _renderers, Vector3 _endPos)
	{
		for (int i = 0; i < _renderers.Length; i++)
		{
			_renderers[i].autodestruct = true;
		}
		_trail.position = _endPos;
		bool hasRenderer = true;
		while (hasRenderer)
		{
			hasRenderer = false;
			for (int j = 0; j < _renderers.Length; j++)
			{
				if (_renderers[j] != null)
				{
					hasRenderer = true;
				}
			}
			yield return null;
		}
		if (_trail != null && _trail.gameObject != null)
		{
			UnityEngine.Object.Destroy(_trail.gameObject);
		}
	}

	protected virtual void OnTransition(Trail _trail, Trail.PointInfo _info)
	{
	}

	protected virtual bool IsTrailableTerrain(Vector3 _point, TrailConfig _config)
	{
		GameObject gridOccupantAtPoint = GetGridOccupantAtPoint(_point);
		if (gridOccupantAtPoint == null)
		{
			return false;
		}
		WorldMapTileFlip component = gridOccupantAtPoint.GetComponent<WorldMapTileFlip>();
		if (component == null)
		{
			return false;
		}
		MapNode levelMapNode = component.m_flipOwnerData.m_levelMapNode;
		if (levelMapNode != null && !levelMapNode.Unfolded && !levelMapNode.Unfolding)
		{
			return false;
		}
		WorldMapTileOptimizer component2 = gridOccupantAtPoint.GetComponent<WorldMapTileOptimizer>();
		if (component2 == null)
		{
			return false;
		}
		TileCosmetics cosmetics = component2.Cosmetics;
		if (cosmetics == null)
		{
			return false;
		}
		for (int i = 0; i < _config.m_trailableTiles.Length; i++)
		{
			if (cosmetics.m_name == _config.m_trailableTiles[i])
			{
				return true;
			}
		}
		return false;
	}

	private GameObject GetGridOccupantAtPoint(Vector3 _point)
	{
		return m_controls.GridManager.GetGridOccupant(m_controls.GridManager.GetUnclampedGridLocationFromPos(_point));
	}

	private bool FindTransitionPoint(Vector3 _start, Vector3 _end, out Vector3 _transitionPoint)
	{
		GameObject gridOccupantAtPoint = GetGridOccupantAtPoint(_start);
		HexGridManager hexGridManager = m_controls.GridManager as HexGridManager;
		if (gridOccupantAtPoint != null)
		{
			Vector3 vector = ProjectOntoPlane(_end, gridOccupantAtPoint.transform.position, gridOccupantAtPoint.transform.up);
			Vector3 normalized = (vector - gridOccupantAtPoint.transform.position).normalized;
			float hexRadius = hexGridManager.HexRadius;
			float num = Vector3.Angle(Vector3.right, normalized);
			float num2 = num % 60f;
			float num3 = m_sqrtThree * hexRadius / (m_sqrtThree * Mathf.Cos(num2 * ((float)Math.PI / 180f)) + Mathf.Sin(num2 * ((float)Math.PI / 180f)));
			_transitionPoint = gridOccupantAtPoint.transform.position + normalized * num3;
			return true;
		}
		_transitionPoint = Vector3.zero;
		return false;
	}

	private Vector3 ProjectOntoPlane(Vector3 _point, Vector3 _planePoint, Vector3 _planeNormal)
	{
		return _point - Vector3.Dot(_point, _planeNormal) * _planeNormal;
	}
}
