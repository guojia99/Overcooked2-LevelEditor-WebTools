using System;
using Team17.Online;
using UnityEngine;

public class EmoteWheel : MonoBehaviour
{
	public EmoteWheelOptions m_emoteWheelOptions;

	public Animator m_animationTarget;

	public GameObject m_codeTriggerTarget;

	[SerializeField]
	public bool m_showInPauseMenu;

	[SerializeField]
	[Range(0f, 1f)]
	public float m_analogEnableThreshold = 0.25f;

	[HideInInspector]
	public PlayerInputLookup.Player m_player = PlayerInputLookup.Player.Count;

	[HideInInspector]
	public UIPlayerMenuBehaviour m_uiPlayer;

	[HideInInspector]
	public PlayerControls m_playerControls;

	[HideInInspector]
	public ClientPlayerRespawnBehaviour m_playerRespawn;

	[HideInInspector]
	public PlayerSwitchingManager m_playerSwitchManager;

	[HideInInspector]
	public PlayerIDProvider m_playerIDProvider;

	public bool IsLocal
	{
		get
		{
			if (ForUI)
			{
				int player = (int)m_player;
				if (player < ClientUserSystem.m_Users.Count && m_player != PlayerInputLookup.Player.Count)
				{
					return ClientUserSystem.m_Users._items[player].IsLocal;
				}
				return false;
			}
			return m_playerIDProvider.GetID() != PlayerInputLookup.Player.Count;
		}
	}

	public bool ForUI
	{
		get
		{
			return m_uiPlayer != null;
		}
	}

	public void Awake()
	{
		if (!m_showInPauseMenu && base.transform.root.GetComponentInChildren<InGamePauseMenu>() != null)
		{
			UnityEngine.Object.Destroy(this);
		}
	}

	public void Start()
	{
		Setup();
		ClientUserSystem.usersChanged = (GenericVoid)Delegate.Combine(ClientUserSystem.usersChanged, new GenericVoid(OnUsersChanged));
		SpawnNetworkComponents();
	}

	protected void SpawnNetworkComponents()
	{
		if (ForUI)
		{
			if (ConnectionStatus.IsHost() || !ConnectionStatus.IsInSession())
			{
				base.gameObject.AddComponent<ServerEmoteWheel>();
			}
			base.gameObject.AddComponent<ClientEmoteWheel>();
		}
	}

	private void Setup()
	{
		m_player = GetPlayer();
		if (!ForUI)
		{
			SetupForIngame();
		}
	}

	private void SetupForIngame()
	{
		m_playerControls = base.gameObject.RequireComponent<PlayerControls>();
		m_playerSwitchManager = GameUtils.RequireManager<PlayerSwitchingManager>();
		m_playerIDProvider = base.gameObject.RequireComponent<PlayerIDProvider>();
		m_playerRespawn = base.gameObject.RequestComponent<ClientPlayerRespawnBehaviour>();
	}

	public bool CanShow()
	{
		if (T17DialogBoxManager.HasAnyOpenDialogs())
		{
			return false;
		}
		if (ForUI)
		{
			if (!IsLocal)
			{
				return false;
			}
		}
		else
		{
			if (T17InGameFlow.Instance != null && T17InGameFlow.Instance.IsPauseMenuOpen())
			{
				return false;
			}
			if (TimeManager.IsPaused(TimeManager.PauseLayer.Main) || TimeManager.IsPaused(TimeManager.PauseLayer.Network))
			{
				return false;
			}
			if (!IsLocal)
			{
				return false;
			}
			if (m_playerRespawn != null && m_playerRespawn.IsRespawning)
			{
				return false;
			}
			if (m_playerSwitchManager != null)
			{
				PlayerControls playerControls = m_playerSwitchManager.SelectedAvatar(m_playerIDProvider.GetID());
				return playerControls != null && playerControls == m_playerControls;
			}
		}
		return m_player != PlayerInputLookup.Player.Count;
	}

	protected PlayerInputLookup.Player GetPlayer()
	{
		if (m_uiPlayer == null && m_player == PlayerInputLookup.Player.Count)
		{
			m_uiPlayer = base.gameObject.RequestComponent<UIPlayerMenuBehaviour>();
		}
		if (m_uiPlayer != null)
		{
			int num = ClientUserSystem.m_Users.FindIndex((User user) => user == m_uiPlayer.UserInfo);
			return (num == -1) ? PlayerInputLookup.Player.Count : ((PlayerInputLookup.Player)num);
		}
		if (m_player != PlayerInputLookup.Player.Count)
		{
			return m_player;
		}
		if (ClientUserSystem.m_Users.Count != 0)
		{
			PlayerIDProvider playerIDProvider = base.gameObject.RequestComponent<PlayerIDProvider>();
			if (playerIDProvider != null)
			{
				return playerIDProvider.GetID();
			}
		}
		return PlayerInputLookup.Player.Count;
	}

	protected void OnUsersChanged()
	{
		Setup();
	}

	protected virtual void OnDestroy()
	{
		ClientUserSystem.usersChanged = (GenericVoid)Delegate.Remove(ClientUserSystem.usersChanged, new GenericVoid(OnUsersChanged));
	}
}
