using System;
using System.Collections.Generic;
using GameModes;
using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public abstract class ClientPortalMapNode : ClientSynchroniserBase, IClientMapSelectable
{
	private struct UIInstance
	{
		public WorldMapLevelIconUI m_uiInstance;

		public WorldMapLevelIconUI m_prefab;

		public void Reset()
		{
			m_uiInstance = null;
			m_prefab = null;
		}
	}

	protected enum UIState
	{
		Inactive = 0,
		RequestedUpdate = 1,
		Enabling = 2,
		Enabled = 3,
		Disabled = 4
	}

	private static List<HoverIconUIController> s_allIcons;

	private PortalMapNode m_basePortalMapNode;

	private UIInstance m_uiInstance;

	private string m_label = string.Empty;

	private bool m_inSelectable;

	protected UIState m_UIState;

	protected bool InSelectable
	{
		get
		{
			return m_inSelectable;
		}
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_basePortalMapNode = (PortalMapNode)synchronisedObject;
		m_basePortalMapNode.RegisterPostFlipCallback(OnFlipFinished);
		Mailbox.Client.RegisterForMessageType(MessageType.GameState, OnGameStateChanged);
	}

	public override EntityType GetEntityType()
	{
		return EntityType.PortalMapNode;
	}

	private void OnGameStateChanged(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
	{
		GameStateMessage gameStateMessage = (GameStateMessage)message;
		if (gameStateMessage.m_State == GameState.InMap)
		{
			EnsureIconAllocated();
			m_UIState = UIState.RequestedUpdate;
			NetworkUtils.OnGameProgressLoadedFromNetwork = (GenericVoid)Delegate.Combine(NetworkUtils.OnGameProgressLoadedFromNetwork, new GenericVoid(OnGameProgressLoadedFromNetwork));
		}
	}

	public void OnGameProgressLoadedFromNetwork()
	{
		m_UIState = UIState.RequestedUpdate;
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		if (m_basePortalMapNode != null)
		{
			m_basePortalMapNode.UnregisterPostFlipCallback(OnFlipFinished);
		}
		Mailbox.Client.UnregisterForMessageType(MessageType.GameState, OnGameStateChanged);
		NetworkUtils.OnGameProgressLoadedFromNetwork = (GenericVoid)Delegate.Remove(NetworkUtils.OnGameProgressLoadedFromNetwork, new GenericVoid(OnGameProgressLoadedFromNetwork));
		RemoveIcon();
		if (m_uiInstance.m_uiInstance != null)
		{
			UnityEngine.Object.Destroy(m_uiInstance.m_uiInstance.gameObject);
			m_uiInstance.Reset();
		}
	}

	protected override void OnEnable()
	{
		base.OnEnable();
		if (m_uiInstance.m_uiInstance != null)
		{
			m_uiInstance.m_uiInstance.gameObject.SetActive(true);
		}
		GameSession gameSession = GameUtils.GetGameSession();
		if (gameSession != null)
		{
			gameSession.OnGameModeSessionConfigChanged = (OnSessionConfigChanged)Delegate.Combine(gameSession.OnGameModeSessionConfigChanged, new OnSessionConfigChanged(OnGameModeSessionConfigChanged));
		}
	}

	protected override void OnDisable()
	{
		base.OnDisable();
		if (m_uiInstance.m_uiInstance != null)
		{
			m_uiInstance.m_uiInstance.gameObject.SetActive(false);
		}
		GameSession gameSession = GameUtils.GetGameSession();
		if (gameSession != null)
		{
			gameSession.OnGameModeSessionConfigChanged = (OnSessionConfigChanged)Delegate.Remove(gameSession.OnGameModeSessionConfigChanged, new OnSessionConfigChanged(OnGameModeSessionConfigChanged));
		}
	}

	private void OnGameModeSessionConfigChanged(SessionConfig _config)
	{
		WorldMapLevelIconUI uIPrefab = m_basePortalMapNode.GetUIPrefab(_config.m_kind);
		if (m_uiInstance.m_prefab != uIPrefab || m_inSelectable)
		{
			RecreateIcon();
		}
	}

	public override void UpdateSynchronising()
	{
		switch (m_UIState)
		{
		case UIState.Inactive:
		case UIState.Enabled:
		case UIState.Disabled:
			break;
		case UIState.RequestedUpdate:
		{
			if (!(null != m_basePortalMapNode) || !m_basePortalMapNode.Unfolded || !(null != m_uiInstance.m_uiInstance))
			{
				break;
			}
			if (m_basePortalMapNode.m_uiAlwaysActive)
			{
				m_UIState = UIState.Enabling;
				break;
			}
			bool flag = CalculateOnScreen();
			m_uiInstance.m_uiInstance.gameObject.SetActive(flag);
			if (flag)
			{
				m_UIState = UIState.Enabling;
			}
			else
			{
				m_UIState = UIState.Disabled;
			}
			break;
		}
		case UIState.Enabling:
			if (null != m_uiInstance.m_uiInstance && m_uiInstance.m_uiInstance.IsReady())
			{
				SetupUI(m_uiInstance.m_uiInstance);
				m_uiInstance.m_uiInstance.SetAvatarProximity(InSelectable);
				m_UIState = UIState.Enabled;
			}
			break;
		}
	}

	private void OnFlipFinished(FlipDirection _direction, FlipType _flipType)
	{
		EnsureIconAllocated();
	}

	private void EnsureIconAllocated()
	{
		if (!(null != m_basePortalMapNode))
		{
			return;
		}
		if (m_basePortalMapNode.Unfolded || m_basePortalMapNode.Unfolding)
		{
			if (!(m_uiInstance.m_uiInstance == null))
			{
				return;
			}
			if (s_allIcons == null)
			{
				s_allIcons = new List<HoverIconUIController>();
			}
			Transform transform = base.transform;
			GameSession gameSession = GameUtils.GetGameSession();
			WorldMapLevelIconUI uIPrefab = m_basePortalMapNode.GetUIPrefab(gameSession.GameModeKind);
			m_uiInstance.m_prefab = uIPrefab;
			GameObject obj = GameUtils.InstantiateHoverIconUIController(uIPrefab.gameObject, transform, "HoverIconCanvas");
			m_uiInstance.m_uiInstance = obj.RequireComponent<WorldMapLevelIconUI>();
			if (s_allIcons.Count == 0)
			{
				s_allIcons.Add(m_uiInstance.m_uiInstance);
				return;
			}
			int num = FindClosestIcon(m_uiInstance.m_uiInstance);
			int num2 = 0;
			HoverIconUIController hoverIconUIController = s_allIcons[num];
			if (hoverIconUIController.GetFollowTransform().position.z >= transform.position.z)
			{
				num2 = 1;
			}
			s_allIcons.Insert(num + num2, m_uiInstance.m_uiInstance);
			m_uiInstance.m_uiInstance.transform.SetSiblingIndex(hoverIconUIController.transform.GetSiblingIndex() + num2);
		}
		else
		{
			RemoveIcon();
			if (m_uiInstance.m_uiInstance != null)
			{
				UnityEngine.Object.Destroy(m_uiInstance.m_uiInstance.gameObject);
				m_uiInstance.Reset();
			}
		}
	}

	private int FindClosestIcon(HoverIconUIController _icon)
	{
		int num = 0;
		int num2 = s_allIcons.Count - 1;
		int num3 = 0;
		float z = _icon.GetFollowTransform().position.z;
		while (num <= num2)
		{
			num3 = num + (num2 - num) / 2;
			float z2 = s_allIcons[num3].GetFollowTransform().position.z;
			if (z2 > z)
			{
				num = num3 + 1;
			}
			else
			{
				num2 = num3 - 1;
			}
			if (Mathf.Approximately(z2, z))
			{
				return num3;
			}
		}
		return num3;
	}

	private bool CalculateOnScreen()
	{
		Vector2 vector = Camera.main.WorldToViewportPoint(base.transform.position);
		return vector.x > -0.05f && vector.x < 1.05f && vector.y > -0.1f && vector.y < 1f;
	}

	private void RemoveIcon()
	{
		if (s_allIcons == null)
		{
			return;
		}
		if (m_uiInstance.m_uiInstance != null)
		{
			s_allIcons.Remove(m_uiInstance.m_uiInstance);
		}
		else
		{
			for (int num = s_allIcons.Count - 1; num >= 0; num--)
			{
				if (s_allIcons[num] == null)
				{
					s_allIcons.RemoveAt(num);
				}
			}
		}
		if (s_allIcons.Count == 0)
		{
			s_allIcons = null;
		}
	}

	private void RecreateIcon()
	{
		RemoveIcon();
		if (m_uiInstance.m_uiInstance != null)
		{
			UnityEngine.Object.Destroy(m_uiInstance.m_uiInstance.gameObject);
			m_uiInstance.Reset();
		}
		EnsureIconAllocated();
		m_UIState = UIState.RequestedUpdate;
	}

	protected abstract void SetupUI(WorldMapLevelIconUI _ui);

	public void AvatarEnteringSelectable(MapAvatarControls _avatar)
	{
		m_inSelectable = true;
		m_UIState = UIState.RequestedUpdate;
		GameSession gameSession = GameUtils.GetGameSession();
		if (gameSession != null && m_uiInstance.m_prefab != m_basePortalMapNode.GetUIPrefab(gameSession.GameModeKind))
		{
			RecreateIcon();
		}
	}

	public void AvatarLeavingSelectable(MapAvatarControls _avatar)
	{
		m_inSelectable = false;
		m_UIState = UIState.RequestedUpdate;
	}
}
