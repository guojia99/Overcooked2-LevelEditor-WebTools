using UnityEngine;

public class Travelator : MonoBehaviour, IMovingSurface
{
	public enum XZDirection
	{
		Leftwards = 0,
		Rightwards = 1
	}

	[SerializeField]
	public float m_speed = 1f;

	[SerializeField]
	private XZDirection m_directionXZ = XZDirection.Rightwards;

	[SerializeField]
	private float m_border;

	[SerializeField]
	public float m_materialScrollMultiplier = 0.3f;

	private GridManager m_gridManager;

	private GridIndex m_gridIndex = default(GridIndex);

	private int m_shaderSpeedProp = Shader.PropertyToID("_speed");

	private void Start()
	{
		StaticGridLocation staticGridLocation = base.gameObject.RequireComponent<StaticGridLocation>();
		m_gridManager = staticGridLocation.AccessGridManager;
		m_gridIndex = staticGridLocation.GridIndex;
		SetSpeedOnMaterial();
	}

	private void SetSpeedOnMaterial()
	{
		MeshRenderer componentInChildren = base.gameObject.GetComponentInChildren<MeshRenderer>();
		Material[] materials = componentInChildren.materials;
		for (int i = 0; i < materials.Length; i++)
		{
			if (componentInChildren.materials[i].HasProperty(m_shaderSpeedProp))
			{
				componentInChildren.materials[i].SetFloat(m_shaderSpeedProp, m_speed * m_materialScrollMultiplier);
			}
		}
	}

	public Vector3 CalculateVelocityAtPoint(Vector3 _point, IMovingSurface _prevSurface)
	{
		Vector3 _velocity = Vector3.zero;
		if (CalculateForPrevSurface(_prevSurface, _point, ref _velocity))
		{
			return _velocity;
		}
		if (CalculateForBorders(_point, ref _velocity))
		{
			return _velocity;
		}
		return GetSurfaceVelocity();
	}

	public Quaternion CalculateRotationAtPoint(Vector3 _point, Quaternion _prevRotation)
	{
		return _prevRotation;
	}

	private bool CalculateForPrevSurface(IMovingSurface _surface, Vector3 _point, ref Vector3 _velocity)
	{
		if (_surface == null || _surface == this)
		{
			return false;
		}
		Travelator travelator = _surface as Travelator;
		if (travelator == null)
		{
			return false;
		}
		if (travelator.GetNextTravelator() == this)
		{
			Vector3 travelDirection = GetTravelDirection();
			Vector3 travelDirection2 = travelator.GetTravelDirection();
			if (Vector3.Dot(travelDirection, travelDirection2) < 0.5f)
			{
				float num = VectorUtils.ProgressUnclamped(travelator.transform.position, base.transform.position, _point);
				if (num >= 0f && num <= 1f)
				{
					_velocity = travelator.GetSurfaceVelocity();
					return true;
				}
			}
		}
		return false;
	}

	private bool CalculateForBorders(Vector3 _point, ref Vector3 _velocity)
	{
		Vector3 borderDirection = GetBorderDirection(_point);
		if (borderDirection.sqrMagnitude > 0f)
		{
			Travelator adjacentTravelator = GetAdjacentTravelator(borderDirection);
			if (adjacentTravelator != null && adjacentTravelator.GetNextTravelator() == this)
			{
				_velocity = adjacentTravelator.GetSurfaceVelocity();
				return true;
			}
		}
		return false;
	}

	private Vector3 GetBorderDirection(Vector3 _point)
	{
		Vector3 vector = _point - base.transform.position;
		if (vector.x >= m_border)
		{
			return Vector3.right;
		}
		if (vector.x <= 0f - m_border)
		{
			return Vector3.left;
		}
		if (vector.z >= m_border)
		{
			return Vector3.forward;
		}
		if (vector.z <= 0f - m_border)
		{
			return Vector3.back;
		}
		return Vector3.zero;
	}

	private Vector3 GetSurfaceVelocity()
	{
		return (!base.enabled) ? Vector3.zero : (m_speed * GetTravelDirection());
	}

	private Travelator GetNextTravelator()
	{
		Vector3 travelDirection = GetTravelDirection();
		if (travelDirection.sqrMagnitude > 0f)
		{
			return GetAdjacentTravelator(travelDirection);
		}
		return null;
	}

	private Travelator GetAdjacentTravelator(Vector3 _directionXZ)
	{
		GridIndex adjacentGridIndex = GetAdjacentGridIndex(_directionXZ);
		GameObject gridOccupant = m_gridManager.GetGridOccupant(adjacentGridIndex);
		if (gridOccupant != null)
		{
			return gridOccupant.RequestComponent<Travelator>();
		}
		return null;
	}

	private GridIndex GetAdjacentGridIndex(Vector3 _directionXZ)
	{
		int num = (int)Mathf.Round(_directionXZ.x);
		int num2 = (int)Mathf.Round(_directionXZ.z);
		return new GridIndex(m_gridIndex.X + num, m_gridIndex.Y, m_gridIndex.Z + num2);
	}

	private Vector3 GetTravelDirection()
	{
		switch (m_directionXZ)
		{
		case XZDirection.Leftwards:
			return base.transform.right;
		case XZDirection.Rightwards:
			return -base.transform.right;
		default:
			return Vector3.zero;
		}
	}
}
