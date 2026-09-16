using System;
using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class WorldMapRegionTransitioner : MonoBehaviour
{
	public delegate void RegionChangeCallback(WorldMapRegion _oldRegion, WorldMapRegion _newRegion);

	private WorldMapRegion m_currentRegion;

	private RegionChangeCallback m_onRegionChanged;

	private bool m_CanPollRegion;

	public WorldMapRegion CurrentRegion
	{
		get
		{
			return m_currentRegion;
		}
	}

	public void Start()
	{
		Mailbox.Client.RegisterForMessageType(MessageType.GameState, OnGameStateChanged);
	}

	public void OnDestroy()
	{
		Mailbox.Client.UnregisterForMessageType(MessageType.GameState, OnGameStateChanged);
	}

	public void RegisterRegionChangeCallback(RegionChangeCallback _callback)
	{
		m_onRegionChanged = (RegionChangeCallback)Delegate.Combine(m_onRegionChanged, _callback);
	}

	public void UnregisterRegionChangeCallback(RegionChangeCallback _callback)
	{
		m_onRegionChanged = (RegionChangeCallback)Delegate.Remove(m_onRegionChanged, _callback);
	}

	private void OnRegionChanged(WorldMapRegion _newRegion)
	{
		if (!(_newRegion == null))
		{
			if (m_onRegionChanged != null)
			{
				m_onRegionChanged(m_currentRegion, _newRegion);
			}
			m_currentRegion = _newRegion;
		}
	}

	private void OnGameStateChanged(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
	{
		GameStateMessage gameStateMessage = (GameStateMessage)message;
		if (gameStateMessage.m_State == GameState.MapStartEntities)
		{
			m_CanPollRegion = true;
		}
	}

	private void Update()
	{
		if (m_CanPollRegion)
		{
			WorldMapRegion worldMapRegion = WorldMapRegion.FindRegionForPoint(base.transform.position);
			if (worldMapRegion != m_currentRegion)
			{
				OnRegionChanged(worldMapRegion);
			}
		}
	}
}
