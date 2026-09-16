using System;
using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerWorkableItem : ServerSynchroniserBase, ISurfacePlacementNotified
{
	private WorkableItem m_workable;

	private WorkableMessage m_data = new WorkableMessage();

	private int m_chopsPerSlice = 1;

	private bool m_onWorkstation;

	private int m_progress;

	private int m_subProgress;

	public override EntityType GetEntityType()
	{
		return EntityType.Workable;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_workable = (WorkableItem)synchronisedObject;
		NetworkUtils.RegisterSpawnablePrefab(base.gameObject, m_workable.GetNextPrefab());
		m_chopsPerSlice = m_workable.GetChopTimeMultiplier(ServerUserSystem.m_Users.Count);
	}

	private void SynchroniseClientState()
	{
		m_data.m_onWorkstation = m_onWorkstation;
		m_data.m_progress = m_progress;
		m_data.m_subProgress = m_subProgress;
		SendServerEvent(m_data);
	}

	public void OnSurfacePlacement(ServerAttachStation _station)
	{
		m_onWorkstation = true;
		SynchroniseClientState();
	}

	public void OnSurfaceDeplacement(ServerAttachStation _station)
	{
		m_onWorkstation = false;
		SynchroniseClientState();
	}

	public float GetProgress()
	{
		return (float)m_progress / (float)Mathf.Max(m_workable.m_stages - 1, 1);
	}

	public bool HasFinished()
	{
		return m_progress == m_workable.m_stages - 1;
	}

	public void DoWork(ServerAttachStation _station, GameObject _worker)
	{
		if (HasFinished())
		{
			return;
		}
		m_subProgress++;
		if (m_subProgress >= m_chopsPerSlice && !GameUtils.GetDebugConfig().m_infiniteChopping)
		{
			m_subProgress = 0;
			DoWork(_station, _worker, 1);
			if (HasFinished())
			{
				ServerMessenger.Achievement(_worker, 5);
			}
		}
	}

	public void DoWork(ServerAttachStation _station, GameObject _worker, int _progress)
	{
		if (HasFinished())
		{
			return;
		}
		m_subProgress = 0;
		m_progress = Mathf.Min(m_progress + _progress, m_workable.m_stages - 1);
		if (!HasFinished())
		{
			return;
		}
		ServerLimitedQuantityItem serverLimitedQuantityItem = m_workable.gameObject.RequestComponent<ServerLimitedQuantityItem>();
		if (serverLimitedQuantityItem != null)
		{
			serverLimitedQuantityItem.AddInvincibilityCondition(() => true);
		}
		Vector3 localPosition = base.transform.localPosition;
		Quaternion localRotation = base.transform.localRotation;
		IAttachment component = NetworkUtils.ServerSpawnPrefab(base.gameObject, m_workable.GetNextPrefab()).GetComponent<IAttachment>();
		GameObject gameObject = _station.TakeItem();
		_station.AddItem(component.AccessGameObject(), Vector2.up, new PlacementContext(PlacementContext.Source.Player));
		component.AccessGameObject().transform.localPosition = localPosition;
		component.AccessGameObject().transform.localRotation = localRotation;
		NetworkUtils.DestroyObject(base.gameObject);
	}

	private void Awake()
	{
		UserSystemUtils.OnServerChangedGameState = (GenericVoid<GameState, GameStateMessage.GameStatePayload>)Delegate.Combine(UserSystemUtils.OnServerChangedGameState, new GenericVoid<GameState, GameStateMessage.GameStatePayload>(OnGameStateChanged));
	}

	private void OnGameStateChanged(GameState _state, GameStateMessage.GameStatePayload payload)
	{
		if (_state == GameState.StartEntities)
		{
			m_chopsPerSlice = m_workable.GetChopTimeMultiplier(ServerUserSystem.m_Users.Count);
		}
	}

	public override void OnDestroy()
	{
		UserSystemUtils.OnServerChangedGameState = (GenericVoid<GameState, GameStateMessage.GameStatePayload>)Delegate.Remove(UserSystemUtils.OnServerChangedGameState, new GenericVoid<GameState, GameStateMessage.GameStatePayload>(OnGameStateChanged));
		base.OnDestroy();
	}
}
