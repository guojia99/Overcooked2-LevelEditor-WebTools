using System.Collections.Generic;
using UnityEngine;

public class CollisionRecorder : MonoBehaviour
{
	private Generic<bool, Collision> m_collisionFilter;

	private List<Collision> m_collision = new List<Collision>();

	private void OnCollisionStay(Collision _collision)
	{
		if (m_collisionFilter == null || m_collisionFilter(_collision))
		{
			m_collision.Add(_collision);
		}
	}

	public List<Collision> GetRecentCollisions()
	{
		return m_collision;
	}

	private void LateUpdate()
	{
		m_collision.Clear();
	}

	public void SetFilter(Generic<bool, Collision> _filterFunction)
	{
		m_collisionFilter = _filterFunction;
	}
}
