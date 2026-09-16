using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerBoundContainerBehaviour : ServerSynchroniserBase
{
	private BoundContainer m_boundContainer;

	private UtensilRespawnBehaviour[] m_utensils;

	private float m_xPositionMin;

	private float m_xPositionMax;

	private float m_yPositionMin;

	private float m_yPositionMax;

	private float m_zPostionMin;

	private float m_zPostionMax;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_boundContainer = (BoundContainer)synchronisedObject;
		m_utensils = Object.FindObjectsOfType<UtensilRespawnBehaviour>();
		m_xPositionMin = m_boundContainer.transform.position.x - m_boundContainer.m_boundSize.x / 2f;
		m_xPositionMax = m_boundContainer.transform.position.x + m_boundContainer.m_boundSize.x / 2f;
		m_yPositionMin = m_boundContainer.transform.position.y - m_boundContainer.m_boundSize.y / 2f;
		m_yPositionMax = m_boundContainer.transform.position.y + m_boundContainer.m_boundSize.y / 2f;
		m_zPostionMin = m_boundContainer.transform.position.z - m_boundContainer.m_boundSize.z / 2f;
		m_zPostionMax = m_boundContainer.transform.position.z + m_boundContainer.m_boundSize.z / 2f;
	}

	public override void UpdateSynchronising()
	{
		base.UpdateSynchronising();
		if (m_utensils == null)
		{
			return;
		}
		for (int i = 0; i < m_utensils.Length; i++)
		{
			GameObject gameObject = ((!(m_utensils[i] != null)) ? null : m_utensils[i].gameObject);
			if (gameObject != null && !IsInBound(gameObject.transform.position) && gameObject.activeInHierarchy)
			{
				ServerPlayerRespawnManager.KillOrRespawn(gameObject, null);
			}
		}
	}

	private bool IsInBound(Vector3 position)
	{
		if (position.x > m_xPositionMin && position.x < m_xPositionMax && position.y > m_yPositionMin && position.y < m_yPositionMax && position.z > m_zPostionMin && position.z < m_zPostionMax)
		{
			return true;
		}
		return false;
	}
}
