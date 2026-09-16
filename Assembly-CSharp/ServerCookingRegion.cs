using System.Collections.Generic;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerCookingRegion : ServerSynchroniserBase
{
	private CookingRegion m_CookingRegion;

	private TriggerRecorder m_recorder;

	private GridManager m_gridManager;

	private GridIndex m_gridIndex;

	private List<ICookable> m_cookedThisFrame = new List<ICookable>();

	public override EntityType GetEntityType()
	{
		return EntityType.CookingRegion;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_CookingRegion = (CookingRegion)synchronisedObject;
		m_gridManager = GameUtils.GetGridManager(base.transform);
		m_gridIndex = m_gridManager.GetGridLocationFromPos(base.transform.position);
		m_recorder = base.gameObject.RequireComponent<TriggerRecorder>();
	}

	public override void UpdateSynchronising()
	{
		base.UpdateSynchronising();
		if (m_CookingRegion == null || !m_CookingRegion.enabled)
		{
			return;
		}
		List<Collider> recentCollisions = m_recorder.GetRecentCollisions();
		for (int i = 0; i < recentCollisions.Count; i++)
		{
			Collider collider = recentCollisions[i];
			ICookable cookable = collider.gameObject.RequestInterface<ICookable>();
			if (cookable == null || cookable.GetRequiredStationType() != m_CookingRegion.m_StationType || m_cookedThisFrame.Contains(cookable))
			{
				continue;
			}
			GridManager gridManager = GameUtils.GetGridManager(collider.transform);
			Vector3 position = collider.transform.position;
			if (!(gridManager != m_gridManager) && !(gridManager.GetGridLocationFromPos(position) != m_gridIndex))
			{
				IOrderDefinition orderDefinition = collider.gameObject.RequestInterface<IOrderDefinition>();
				if (orderDefinition.GetOrderComposition().Simpilfy() != AssembledDefinitionNode.NullNode)
				{
					cookable.Cook(TimeManager.GetDeltaTime(base.gameObject.layer));
					m_cookedThisFrame.Add(cookable);
				}
			}
		}
		m_cookedThisFrame.Clear();
	}
}
