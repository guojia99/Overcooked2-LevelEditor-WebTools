using System.Collections.Generic;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerTriggerMoveSpawnPoints : ServerSynchroniserBase, ITriggerReceiver
{
	private TriggerMoveSpawnPoints m_changeRespawn;

	private MoveSpawnMessage m_data = new MoveSpawnMessage();

	public override EntityType GetEntityType()
	{
		return EntityType.TriggerMoveSpawn;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_changeRespawn = (TriggerMoveSpawnPoints)synchronisedObject;
	}

	public void OnTrigger(string _trigger)
	{
		if (m_changeRespawn.m_trigger == _trigger)
		{
			Move();
		}
	}

	private void Move()
	{
		if (m_changeRespawn.m_spawnPoints != null && m_changeRespawn.m_spawnPoints.Length != 0)
		{
			GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
			KeyValuePair<GameObject, Transform>[] spawns = CalculateBestSpawns(players, m_changeRespawn.m_spawnPoints);
			m_data.Initialise(spawns, m_changeRespawn.m_spawnPoints);
			SendServerEvent(m_data);
		}
	}

	private KeyValuePair<GameObject, Transform>[] CalculateBestSpawns(GameObject[] _players, Transform[] _spawns)
	{
		List<KeyValuePair<GameObject, Transform>> list = new List<KeyValuePair<GameObject, Transform>>();
		for (int i = 0; i < _players.Length; i++)
		{
			list.AddRange(m_changeRespawn.m_spawnPoints.ConvertAll((Transform x) => new KeyValuePair<GameObject, Transform>(_players[i], x)));
		}
		list.Sort(SortSpawnsByDistance);
		KeyValuePair<GameObject, Transform>[] _array = new KeyValuePair<GameObject, Transform>[0];
		for (int num = 0; num < _players.Length; num++)
		{
			KeyValuePair<GameObject, Transform> spawn = list[0];
			list.RemoveAll((KeyValuePair<GameObject, Transform> x) => x.Key == spawn.Key || x.Value == spawn.Value);
			ArrayUtils.PushBack(ref _array, spawn);
		}
		return _array;
	}

	private int SortSpawnsByDistance(KeyValuePair<GameObject, Transform> _spawn1, KeyValuePair<GameObject, Transform> _spawn2)
	{
		bool flag = IsSpawnPointOccupied(_spawn1.Value);
		bool flag2 = IsSpawnPointOccupied(_spawn2.Value);
		if (flag != flag2)
		{
			return flag ? 1 : (-1);
		}
		float sqrMagnitude = (_spawn1.Value.position - _spawn1.Key.transform.position).sqrMagnitude;
		float sqrMagnitude2 = (_spawn2.Value.position - _spawn2.Key.transform.position).sqrMagnitude;
		return sqrMagnitude.CompareTo(sqrMagnitude2);
	}

	private bool IsSpawnPointOccupied(Transform _transform)
	{
		GridManager gridManager = GameUtils.GetGridManager(_transform);
		return gridManager != null && (bool)gridManager.GetGridOccupant(gridManager.GetGridLocationFromPos(_transform.position));
	}
}
