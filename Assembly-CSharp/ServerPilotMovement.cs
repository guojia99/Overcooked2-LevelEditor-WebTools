using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerPilotMovement : ServerSynchroniserBase, IAssignControls
{
	protected PlayerControls.ControlSchemeData m_controlScheme;

	private PilotMovement m_pilotMovement;

	private GridManager m_gridManager;

	private Vector3? m_gridTarget;

	private Collider m_collider;

	private GridIndex m_min;

	private GridIndex m_max;

	private Vector3 m_extents;

	private const float k_castDistance = 0.3f;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_pilotMovement = (PilotMovement)synchronisedObject;
		m_gridManager = GameUtils.GetGridManager(base.transform.parent);
		m_collider = base.gameObject.RequestComponentRecursive<Collider>();
		if (m_collider != null)
		{
			m_extents = new Vector3(m_collider.bounds.extents.x - 0.15f, m_collider.bounds.extents.y, m_collider.bounds.extents.z - 0.15f);
			GridIndex gridLocationFromPos = m_gridManager.GetGridLocationFromPos(m_collider.bounds.center - m_extents);
			GridIndex gridLocationFromPos2 = m_gridManager.GetGridLocationFromPos(m_collider.bounds.center + m_extents);
			if (m_gridManager.TryOccupyGridRegion(gridLocationFromPos, gridLocationFromPos2, base.gameObject))
			{
				m_min = gridLocationFromPos;
				m_max = gridLocationFromPos2;
			}
		}
	}

	public void AssignPlayer(PlayerControls.ControlSchemeData _controlScheme)
	{
		m_controlScheme = _controlScheme;
	}

	private Vector3 GetNearestGridPosition()
	{
		GridIndex gridLocationFromPos = m_gridManager.GetGridLocationFromPos(base.transform.position);
		return m_gridManager.GetPosFromGridLocation(gridLocationFromPos);
	}

	public override void UpdateSynchronising()
	{
		float deltaTime = TimeManager.GetDeltaTime(base.gameObject);
		if (!m_pilotMovement)
		{
			return;
		}
		m_pilotMovement.RigidbodyMotion.SetKinematic(false);
		if (m_controlScheme != null && Update_Movement(deltaTime))
		{
			m_gridTarget = null;
			return;
		}
		if (!m_gridTarget.HasValue)
		{
			m_gridTarget = GetNearestGridPosition();
		}
		float snapHalfLife = m_pilotMovement.SnapHalfLife;
		float num = Mathf.Log(2f) / snapHalfLife;
		Vector3 velocity = (m_gridTarget.Value - base.transform.position) * num;
		m_pilotMovement.RigidbodyMotion.SetVelocity(velocity);
		if (velocity.sqrMagnitude > 0.3f)
		{
			m_gridManager.DeoccupyGridRegion(m_min, m_max);
			GridIndex gridLocationFromPos = m_gridManager.GetGridLocationFromPos(m_collider.bounds.center - m_extents);
			GridIndex gridLocationFromPos2 = m_gridManager.GetGridLocationFromPos(m_collider.bounds.center + m_extents);
			if (m_gridManager.TryOccupyGridRegion(gridLocationFromPos, gridLocationFromPos2, base.gameObject))
			{
				m_min = gridLocationFromPos;
				m_max = gridLocationFromPos2;
			}
			else if (m_gridManager.TryOccupyGridRegion(m_min, m_max, base.gameObject))
			{
			}
		}
	}

	private bool GridBoxCast(ref GridIndex min, ref GridIndex max, Vector3 centre, Vector3 extents, Vector3 direction, float distance, GridManager gridManager)
	{
		Vector3 vector = centre + direction * distance;
		GridIndex gridLocationFromPos = m_gridManager.GetGridLocationFromPos(vector - extents);
		GridIndex gridLocationFromPos2 = m_gridManager.GetGridLocationFromPos(vector + extents);
		if (m_gridManager.TryOccupyGridRegion(gridLocationFromPos, gridLocationFromPos2, base.gameObject))
		{
			min = gridLocationFromPos;
			max = gridLocationFromPos2;
			return true;
		}
		return false;
	}

	private bool Update_Movement(float _deltaTime)
	{
		float value = m_controlScheme.m_moveX.GetValue();
		float z = 0f - m_controlScheme.m_moveY.GetValue();
		Vector3 vector = new Vector3(value, 0f, z).SafeNormalised(Vector3.zero);
		if (vector.sqrMagnitude > 0.040000003f)
		{
			if (m_collider != null)
			{
				Vector3 zero = Vector3.zero;
				m_gridManager.DeoccupyGridRegion(m_min, m_max);
				if (GridBoxCast(ref m_min, ref m_max, m_collider.bounds.center, m_extents, vector, 0.3f, m_gridManager))
				{
					m_pilotMovement.RigidbodyMotion.SetVelocity(m_pilotMovement.MoveSpeed * vector);
					return true;
				}
				if (Mathf.Abs(vector.x) > 0.3f && GridBoxCast(ref m_min, ref m_max, m_collider.bounds.center, m_extents, new Vector3(Mathf.Sign(vector.x), 0f, 0f), 0.3f, m_gridManager))
				{
					m_pilotMovement.RigidbodyMotion.SetVelocity(new Vector3(m_pilotMovement.MoveSpeed * Mathf.Sign(vector.x), 0f, 0f));
					return true;
				}
				if (Mathf.Abs(vector.z) > 0.3f && GridBoxCast(ref m_min, ref m_max, m_collider.bounds.center, m_extents, new Vector3(0f, 0f, Mathf.Sign(vector.z)), 0.3f, m_gridManager))
				{
					m_pilotMovement.RigidbodyMotion.SetVelocity(new Vector3(0f, 0f, m_pilotMovement.MoveSpeed * Mathf.Sign(vector.z)));
					return true;
				}
				if (!m_gridManager.TryOccupyGridRegion(m_min, m_max, base.gameObject))
				{
				}
				m_pilotMovement.RigidbodyMotion.SetVelocity(Vector3.zero);
			}
			else
			{
				m_pilotMovement.RigidbodyMotion.SetVelocity(m_pilotMovement.MoveSpeed * vector);
			}
			return true;
		}
		return false;
	}
}
