using System.Collections;
using System.Collections.Generic;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientTriggerMoveSpawnPoints : ClientSynchroniserBase
{
	private TriggerMoveSpawnPoints m_changeRespawn;

	private List<IEnumerator> m_moveRoutines = new List<IEnumerator>();

	public override EntityType GetEntityType()
	{
		return EntityType.TriggerMoveSpawn;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_changeRespawn = (TriggerMoveSpawnPoints)synchronisedObject;
	}

	public override void ApplyServerEvent(Serialisable serialisable)
	{
		MoveSpawnMessage moveSpawnMessage = (MoveSpawnMessage)serialisable;
		KeyValuePair<GameObject, Transform>[] array = moveSpawnMessage.ExtractSpawnMap(m_changeRespawn.m_spawnPoints);
		for (int i = 0; i < array.Length; i++)
		{
			ApplySpawnChange(array[i].Key, array[i].Value);
		}
		if (m_changeRespawn.m_movePlayersImmediately)
		{
			for (int j = 0; j < array.Length; j++)
			{
				IEnumerator item = MovePlayer(array[j].Key, array[j].Value);
				m_moveRoutines.Add(item);
			}
		}
	}

	private bool ProcessRoutine(IEnumerator _routine)
	{
		return _routine == null || !_routine.MoveNext();
	}

	public override void UpdateSynchronising()
	{
		base.UpdateSynchronising();
		if (m_moveRoutines.Count > 0)
		{
			m_moveRoutines.RemoveAll(ProcessRoutine);
		}
	}

	private void ApplySpawnChange(GameObject _player, Transform _spawn)
	{
		ClientPlayerRespawnBehaviour clientPlayerRespawnBehaviour = _player.RequireComponent<ClientPlayerRespawnBehaviour>();
		clientPlayerRespawnBehaviour.MoveRespawnPoint(_spawn.localPosition, _spawn.parent);
	}

	private IEnumerator MovePlayer(GameObject _player, Transform _target)
	{
		PlayerControls controls = _player.RequireComponent<PlayerControls>();
		controls.enabled = false;
		controls.Motion.SetKinematic(true);
		DynamicLandscapeParenting dynamicParenting = _player.RequestComponent<DynamicLandscapeParenting>();
		if (dynamicParenting != null)
		{
			dynamicParenting.enabled = false;
		}
		ParticleSystem particles = _player.RequestComponentRecursive<ParticleSystem>();
		if (particles != null)
		{
			particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
		}
		ClientWorldObjectSynchroniser synchroniser = _player.RequestComponent<ClientWorldObjectSynchroniser>();
		if (synchroniser != null)
		{
			synchroniser.Pause();
		}
		Transform transform = _player.transform;
		transform.SetPositionAndRotation(_target.position, transform.rotation);
		transform.SetParent(_target);
		if (ConnectionStatus.IsHost() || !ConnectionStatus.IsInSession())
		{
			ServerWorldObjectSynchroniser serverWorldObjectSynchroniser = _player.RequestComponent<ServerWorldObjectSynchroniser>();
			if (serverWorldObjectSynchroniser != null)
			{
				serverWorldObjectSynchroniser.ResumeAllClients();
			}
		}
		yield return null;
		if (synchroniser != null)
		{
			while (!synchroniser.IsReadyToResume())
			{
				yield return null;
			}
			synchroniser.Resume();
		}
		if (particles != null)
		{
			particles.Play();
		}
		if (dynamicParenting != null)
		{
			dynamicParenting.enabled = true;
		}
		controls.enabled = true;
		controls.Motion.SetKinematic(false);
	}
}
