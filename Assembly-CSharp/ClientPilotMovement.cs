using System;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientPilotMovement : ClientSynchroniserBase
{
	private PilotMovement m_pilotMovement;

	private GridManager m_gridManager;

	private Collider m_collider;

	private GridIndex m_min;

	private GridIndex m_max;

	private Vector3 m_extents;

	private const float k_castDistance = 0.3f;

	private GameObject m_avatar;

	public event Action<bool> OnPilotStatusChanged = delegate
	{
	};

	private bool IsClientOnly()
	{
		return !ConnectionStatus.IsHost() && ConnectionStatus.IsInSession();
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_pilotMovement = (PilotMovement)synchronisedObject;
		m_gridManager = GameUtils.RequireManager<GridManager>();
		m_collider = base.gameObject.RequestComponentRecursive<Collider>();
		if (IsClientOnly() && m_collider != null)
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

	public virtual void AssignAvatar(GameObject _avatar)
	{
		if ((bool)m_avatar)
		{
			SetIgnoreCollision(m_avatar, false);
		}
		if ((bool)_avatar)
		{
			SetIgnoreCollision(_avatar, true);
		}
		m_avatar = _avatar;
		this.OnPilotStatusChanged(_avatar != null);
	}

	private void SetIgnoreCollision(GameObject _avatar, bool _shouldIgnore)
	{
		Collider collider = _avatar.RequireComponent<Collider>();
		Collider[] array = base.gameObject.RequestComponentsRecursive<Collider>();
		foreach (Collider collider2 in array)
		{
			if (!collider2.transform.IsChildOf(_avatar.transform))
			{
				Physics.IgnoreCollision(collider, collider2, _shouldIgnore);
			}
		}
	}

	public override void UpdateSynchronising()
	{
		if (IsClientOnly() && m_pilotMovement != null)
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
}
