using System.Collections.Generic;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerTriggerAttachedSpawn : ServerSynchroniserBase, ITriggerReceiver
{
	private TriggerAttachedSpawn m_triggerAttachedSpawn;

	private List<GameObject> m_spawned = new List<GameObject>();

	private ServerAttachStation m_attachStation;

	private IConveyenceReceiver m_conveyenceReceiver;

	private int m_lastSpawned = -1;

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_triggerAttachedSpawn = (TriggerAttachedSpawn)synchronisedObject;
		m_attachStation = FindAttachStation();
		if (m_attachStation != null)
		{
			m_conveyenceReceiver = m_attachStation.gameObject.RequestInterface<IConveyenceReceiver>();
		}
	}

	protected override void OnEnable()
	{
		base.OnEnable();
		if (m_attachStation == null && m_triggerAttachedSpawn != null)
		{
			m_attachStation = FindAttachStation();
			if (m_attachStation != null)
			{
				m_conveyenceReceiver = m_attachStation.gameObject.RequestInterface<IConveyenceReceiver>();
			}
		}
	}

	public void OnTrigger(string _trigger)
	{
		if (!(m_triggerAttachedSpawn != null) || !(m_triggerAttachedSpawn.m_trigger == _trigger) || m_attachStation == null || m_attachStation.HasItem() || (m_conveyenceReceiver != null && m_conveyenceReceiver.IsReceiving()))
		{
			return;
		}
		m_spawned.RemoveAll((GameObject obj) => obj == null);
		if (m_spawned.Count < m_triggerAttachedSpawn.m_maxNumber)
		{
			TriggerAttachedSpawn.WeightedPrefab weightedPrefab;
			if (m_triggerAttachedSpawn.m_spawnInOrder)
			{
				int num = MathUtils.Wrap(m_lastSpawned + 1, 0, m_triggerAttachedSpawn.m_attachmentPrefabs.Length);
				weightedPrefab = m_triggerAttachedSpawn.m_attachmentPrefabs[num];
				m_lastSpawned = num;
			}
			else
			{
				weightedPrefab = m_triggerAttachedSpawn.m_attachmentPrefabs.GetWeightedRandomElement().Value;
			}
			if (weightedPrefab != null)
			{
				GameObject gameObject = NetworkUtils.ServerSpawnPrefab(base.gameObject, weightedPrefab.AttachmentPrefab, m_triggerAttachedSpawn.m_gridPointSelector.position, m_triggerAttachedSpawn.m_gridPointSelector.rotation);
				gameObject.name = weightedPrefab.AttachmentPrefab.name;
				m_attachStation.AddItem(gameObject, m_triggerAttachedSpawn.m_gridPointSelector.forward.XZ());
				m_spawned.Add(gameObject);
			}
		}
	}

	private ServerAttachStation FindAttachStation()
	{
		GridManager gridManager = GameUtils.GetGridManager(base.transform);
		GridIndex gridLocationFromPos = gridManager.GetGridLocationFromPos(m_triggerAttachedSpawn.m_gridPointSelector.position);
		GameObject gridOccupant = gridManager.GetGridOccupant(gridLocationFromPos);
		if (gridOccupant != null)
		{
			return gridOccupant.RequireComponent<ServerAttachStation>();
		}
		return null;
	}
}
