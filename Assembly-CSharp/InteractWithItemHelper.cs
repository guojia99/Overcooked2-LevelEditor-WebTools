using System;
using System.Collections.Generic;
using UnityEngine;

public static class InteractWithItemHelper
{
	public delegate bool ScanCondition(GameObject _testObject);

	public delegate bool ScanCondition<ObjectType>(ObjectType _testObject) where ObjectType : MonoBehaviour;

	private struct GridColliderData
	{
		public Collider Subject;

		public GridIndex Index;

		public GridManager Manager;
	}

	private static GridIndex[] s_gridOffsetsXZ = new GridIndex[4]
	{
		new GridIndex(1, 0, 0),
		new GridIndex(-1, 0, 0),
		new GridIndex(0, 0, 1),
		new GridIndex(0, 0, -1)
	};

	private const int c_defaultGridColliderSize = 16;

	private static List<GridColliderData> s_gridColliderData = new List<GridColliderData>(16);

	private static GridIndex[] GetGridOffsets()
	{
		return s_gridOffsetsXZ;
	}

	public static Collider GetFacingGridOccupant(Transform _scanner, int _layermask)
	{
		s_gridColliderData.Clear();
		int activeCount = GridManager.GetActiveCount();
		for (int i = 0; i < activeCount; i++)
		{
			GridManager active = GridManager.GetActive(i);
			GridIndex unclampedGridLocationFromPos = active.GetUnclampedGridLocationFromPos(_scanner.position);
			GameObject gridOccupant = active.GetGridOccupant(unclampedGridLocationFromPos);
			if (gridOccupant != null && !gridOccupant.CompareTag("Hazard") && !gridOccupant.CompareTag("Travelator") && !gridOccupant.CompareTag("MovingPlatform"))
			{
				continue;
			}
			GridIndex[] gridOffsets = GetGridOffsets();
			for (int j = 0; j < gridOffsets.Length; j++)
			{
				Collider colliderAtGridIndex = GetColliderAtGridIndex(active, unclampedGridLocationFromPos + gridOffsets[j], _layermask);
				if (colliderAtGridIndex != null)
				{
					GridColliderData item = new GridColliderData
					{
						Manager = active,
						Index = gridOffsets[j],
						Subject = colliderAtGridIndex
					};
					s_gridColliderData.Add(item);
				}
			}
		}
		GridColliderData? gridColliderData = null;
		float num = 0f;
		for (int k = 0; k < s_gridColliderData.Count; k++)
		{
			float num2 = ScoreFacingGridCollider(s_gridColliderData[k], _scanner.forward);
			if (!gridColliderData.HasValue || num2 < num)
			{
				gridColliderData = s_gridColliderData[k];
				num = num2;
			}
		}
		s_gridColliderData.Clear();
		if (gridColliderData.HasValue)
		{
			return gridColliderData.Value.Subject;
		}
		return null;
	}

	private static float ScoreFacingGridCollider(GridColliderData _data, Vector3 _forward)
	{
		Vector2 lhs = _data.Manager.transform.InverseTransformDirection(_forward).XZ();
		float num = Vector2.Dot(lhs, new Vector2(_data.Index.X, _data.Index.Z));
		if (num < Mathf.Cos((float)Math.PI * 3f / 4f))
		{
			return float.MaxValue;
		}
		return 1f - num;
	}

	private static Collider GetColliderAtGridIndex(GridManager _gridManager, GridIndex _index, int _layermask)
	{
		GameObject gridOccupant = _gridManager.GetGridOccupant(_index);
		if (gridOccupant != null && gridOccupant.GetComponent<Collider>() != null && ((_layermask & (1 << gridOccupant.layer)) != 0 || _layermask == -1))
		{
			return gridOccupant.GetComponent<Collider>();
		}
		return null;
	}

	public static bool OwnedByGrid(Transform _test)
	{
		if (ComponentCache<IGridLocation>.GetComponent(_test.gameObject) != null)
		{
			return true;
		}
		if (_test.parent != null)
		{
			return OwnedByGrid(_test.parent);
		}
		return false;
	}

	public static Vector3 GetClosestPointOnSurface(Collider _collider, Vector3 _position)
	{
		Transform transform = _collider.transform;
		SphereCollider sphereCollider = _collider as SphereCollider;
		if (sphereCollider != null)
		{
			Vector3 vector = transform.TransformPoint(sphereCollider.center);
			return vector + (_position - vector).normalized * sphereCollider.radius;
		}
		CapsuleCollider capsuleCollider = _collider as CapsuleCollider;
		if (capsuleCollider != null)
		{
			Vector3 vector2 = transform.InverseTransformPoint(_position) - capsuleCollider.center;
			float num = Mathf.Max(capsuleCollider.height - 2f * capsuleCollider.radius, 0f);
			Vector3 vector3;
			if (vector2.y >= -0.5f * num && vector2.y <= 0.5f * num)
			{
				vector3 = (vector2.WithY(0f).normalized * capsuleCollider.radius).WithY(vector2.y);
			}
			else if (vector2.y > 0.5f * num)
			{
				Vector3 vector4 = new Vector3(0f, 0.5f * num, 0f);
				vector3 = vector4 + (vector2 - vector4).normalized * capsuleCollider.radius;
			}
			else
			{
				Vector3 vector5 = new Vector3(0f, -0.5f * num, 0f);
				vector3 = vector5 + (vector2 - vector5).normalized * capsuleCollider.radius;
			}
			return transform.TransformPoint(vector3 + capsuleCollider.center);
		}
		return _collider.ClosestPointOnBounds(_position);
	}

	public static bool IsColliderInArc(Collider _collider, Vector3 _position, Vector3 _forward, float _radius, float _arc)
	{
		Vector3 closestPointOnSurface = GetClosestPointOnSurface(_collider, _position);
		float num = Mathf.Cos(0.5f * _arc);
		Vector3 vector = (closestPointOnSurface - _position).WithY(0f);
		if (vector.sqrMagnitude > 0.001f && vector.sqrMagnitude < _radius * _radius && Vector3.Dot(_forward, vector.normalized) >= num)
		{
			return true;
		}
		return false;
	}

	public static Vector3 GetCollidersInArc(float detectionRadius, float arc, Transform _transform, Collider[] _colliders, int _layerMask, bool _gridSelection)
	{
		Vector3 position = _transform.position;
		Vector3 normalized = _transform.forward.WithY(0f).normalized;
		int num = Physics.OverlapSphereNonAlloc(position, 2f * detectionRadius, _colliders, _layerMask);
		for (int i = 0; i < num; i++)
		{
			Collider collider = _colliders[i];
			if (_transform.gameObject == collider.gameObject)
			{
				_colliders[i] = null;
			}
			else if (!IsColliderInArc(collider, position, normalized, detectionRadius, arc))
			{
				_colliders[i] = null;
			}
			else if (_gridSelection && OwnedByGrid(collider.transform))
			{
				_colliders[i] = null;
			}
		}
		if (_gridSelection)
		{
			Collider facingGridOccupant = GetFacingGridOccupant(_transform, _layerMask);
			if (facingGridOccupant != null)
			{
				bool flag = false;
				for (int j = 0; j < _colliders.Length; j++)
				{
					Collider collider2 = _colliders[j];
					if (collider2 == null)
					{
						_colliders[j] = facingGridOccupant;
						flag = true;
						if (j >= num)
						{
							num = j;
						}
						break;
					}
				}
				if (flag)
				{
				}
			}
		}
		for (int k = num; k < _colliders.Length; k++)
		{
			_colliders[k] = null;
		}
		return position + 0.5f * detectionRadius * normalized;
	}

	public static GameObject ScanForObject(Collider[] _colliders, Vector3 _targetPosition, ScanCondition _condition = null)
	{
		float num = float.PositiveInfinity;
		GameObject result = null;
		foreach (Collider collider in _colliders)
		{
			if (!(collider != null))
			{
				continue;
			}
			GameObject gameObject = collider.gameObject;
			bool flag = _condition == null || _condition(gameObject);
			if (gameObject.activeInHierarchy && flag)
			{
				float sqrMagnitude = (_targetPosition - gameObject.transform.position).sqrMagnitude;
				if (sqrMagnitude < num)
				{
					num = sqrMagnitude;
					result = gameObject;
				}
			}
		}
		return result;
	}

	public static ObjectType ScanForComponent<ObjectType>(Collider[] _colliders, Vector3 _targetPosition, ScanCondition<ObjectType> _condition = null) where ObjectType : MonoBehaviour
	{
		ObjectType result = (ObjectType)null;
		float num = float.PositiveInfinity;
		foreach (Collider collider in _colliders)
		{
			if (!(collider != null))
			{
				continue;
			}
			ObjectType[] components = ComponentCache<ObjectType>.GetComponents(collider.gameObject);
			for (int j = 0; j < components.Length; j++)
			{
				ObjectType val = components[j];
				if (val.enabled && (_condition == null || _condition(val)))
				{
					float sqrMagnitude = (_targetPosition - val.transform.position).sqrMagnitude;
					if (sqrMagnitude < num)
					{
						num = sqrMagnitude;
						result = val;
					}
				}
			}
		}
		return result;
	}
}
