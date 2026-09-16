using System.Collections.Generic;
using BitStream;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class MoveSpawnMessage : Serialisable
{
	private const int c_BitsPerMapLength = 3;

	private const int c_BitsPerSpawnPoint = 6;

	private GameObject[] m_players;

	private int[] m_indexes;

	private EntityMessageHeader m_entityHeader = new EntityMessageHeader();

	public void Initialise(KeyValuePair<GameObject, Transform>[] _spawns, Transform[] _spawnPoints)
	{
		m_players = new GameObject[_spawns.Length];
		m_indexes = new int[_spawns.Length];
		for (int i = 0; i < _spawns.Length; i++)
		{
			m_players[i] = _spawns[i].Key;
			m_indexes[i] = _spawnPoints.FindIndex_Predicate((Transform x) => x == _spawns[i].Value);
		}
	}

	public KeyValuePair<GameObject, Transform>[] ExtractSpawnMap(Transform[] _spawnPoints)
	{
		int num = m_players.Length;
		KeyValuePair<GameObject, Transform>[] array = new KeyValuePair<GameObject, Transform>[num];
		for (int i = 0; i < num; i++)
		{
			array[i] = new KeyValuePair<GameObject, Transform>(m_players[i], _spawnPoints[m_indexes[i]]);
		}
		return array;
	}

	public void Serialise(BitStreamWriter _writer)
	{
		_writer.Write((uint)m_players.Length, 3);
		for (int i = 0; i < m_players.Length; i++)
		{
			EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(m_players[i]);
			entry.m_Header.Serialise(_writer);
			_writer.Write((uint)m_indexes[i], 6);
		}
	}

	public bool Deserialise(BitStreamReader _reader)
	{
		int num = (int)_reader.ReadUInt32(3);
		m_players = new GameObject[num];
		m_indexes = new int[num];
		for (int i = 0; i < num; i++)
		{
			m_entityHeader.Deserialise(_reader);
			EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(m_entityHeader.m_uEntityID);
			m_players[i] = entry.m_GameObject;
			m_indexes[i] = (int)_reader.ReadUInt32(6);
		}
		return true;
	}
}
