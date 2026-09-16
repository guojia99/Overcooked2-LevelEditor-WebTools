using UnityEngine;

[AddComponentMenu("Scripts/Game/Environment/Steppable")]
[RequireComponent(typeof(Collider))]
public class Steppable : MonoBehaviour
{
	[SerializeField]
	[AssignChild("PlaneTransform", Editorbility.NonEditable)]
	private Transform m_planeTransform;

	private Plane m_plane;

	private Collider m_collider;

	private float c_raycastMaxDistance = float.MaxValue;

	private void Awake()
	{
		m_collider = base.gameObject.RequireComponent<Collider>();
		m_plane = new Plane(m_planeTransform.up, m_planeTransform.position);
	}

	public Vector3 ProjectPointOntoStep(Vector3 _point, Vector3 _directionXYZ)
	{
		Vector3 _hit;
		if (RaycastOntoPlane(_point, Vector3.up, out _hit))
		{
			return _hit;
		}
		return _point;
	}

	private bool RaycastOntoCollision(Vector3 _point, Vector3 _direction, out Vector3 _hit)
	{
		Ray ray = new Ray(_point, _direction);
		RaycastHit hitInfo;
		if (m_collider.Raycast(ray, out hitInfo, c_raycastMaxDistance))
		{
			_hit = hitInfo.point;
			return true;
		}
		Debug.DrawRay(ray.origin, ray.direction, Color.red);
		_hit = _point;
		return false;
	}

	private bool RaycastOntoPlane(Vector3 _point, Vector3 _direction, out Vector3 _hit)
	{
		Ray ray = new Ray(_point, _direction);
		float enter = 0f;
		if (!m_plane.Raycast(ray, out enter) && Mathf.Abs(enter) <= 0f)
		{
			_hit = _point;
			return false;
		}
		_hit = ray.origin + ray.direction * enter;
		return true;
	}
}
