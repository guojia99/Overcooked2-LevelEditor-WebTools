using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GridNavigator : MonoBehaviour
{
	[SerializeField]
	private float m_speed = 4.5f;

	private GridNavSpace m_gridNavSpace;

	private bool m_isMoving;

	private float m_speedModifier = 1f;

	private void Awake()
	{
		m_gridNavSpace = GameUtils.GetGridNavSpace();
	}

	public void SetSpeedModifier(float _modifier)
	{
		m_speedModifier = _modifier;
	}

	public void MoveToTarget(Vector3 _pos)
	{
		ClearTarget();
		m_isMoving = true;
		StartCoroutine(MovingCoroutine(_pos));
	}

	public void ClearTarget()
	{
		StopAllCoroutines();
		m_isMoving = false;
	}

	public bool HasCompletedRoute()
	{
		return !m_isMoving;
	}

	private IEnumerator MovingCoroutine(Vector3 _target)
	{
		Point2 startPoint = m_gridNavSpace.GetNavPoint(base.transform.position);
		Point2 targetPoint = m_gridNavSpace.GetNavPoint(_target);
		List<Vector3> path = m_gridNavSpace.FindPath(startPoint, targetPoint);
		if (path.Count > 0)
		{
			float distanceToMove = 0f;
			for (int i = 0; i < path.Count; i++)
			{
				while (true)
				{
					Vector3 nextPos = path[i];
					if (distanceToMove > 0f)
					{
						float magnitude = (nextPos - base.transform.position).magnitude;
						float num = Mathf.Min(magnitude, distanceToMove);
						base.transform.position += (nextPos - base.transform.position).SafeNormalised(Vector3.zero) * num;
						base.transform.rotation = Quaternion.LookRotation((nextPos - base.transform.position).SafeNormalised(base.transform.forward), base.transform.up);
						distanceToMove -= num;
					}
					if (distanceToMove != 0f)
					{
						break;
					}
					yield return null;
					distanceToMove += TimeManager.GetDeltaTime(base.gameObject) * m_speed * m_speedModifier;
				}
			}
		}
		m_isMoving = false;
	}
}
