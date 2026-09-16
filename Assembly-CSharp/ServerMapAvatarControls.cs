using System;
using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerMapAvatarControls : ServerSynchroniserBase
{
	private MapAvatarControls m_avatarControls;

	private Quaternion m_originalVanRotation;

	private ILogicalValue m_moveX;

	private ILogicalValue m_moveY;

	private ILogicalButton m_selectButton;

	private ILogicalButton m_dashButton;

	private float m_dashTimer = float.MinValue;

	private MapAvatarControlsMessage m_ServerData = new MapAvatarControlsMessage();

	private bool m_initialised;

	private MapAvatarGroundCast m_groundCast;

	private Rigidbody m_rigidBody;

	private SaveManager m_saveManager;

	private IServerMapSelectable m_currentSelectable;

	private uint m_currentSelectableEntityID;

	public override EntityType GetEntityType()
	{
		return EntityType.WorldMapVanControls;
	}

	private bool CanButtonBePressed()
	{
		if (T17DialogBoxManager.HasAnyOpenDialogs() || (T17InGameFlow.Instance != null && T17InGameFlow.Instance.m_Rootmenu.GetCurrentOpenMenu() != null))
		{
			return false;
		}
		return true;
	}

	private void InitControls()
	{
		m_moveX = new GateLogicalValue(PlayerInputLookup.GetAnyValueForLocals(PlayerInputLookup.LogicalValueID.MovementX), CanButtonBePressed);
		m_moveY = new GateLogicalValue(PlayerInputLookup.GetAnyValueForLocals(PlayerInputLookup.LogicalValueID.MovementY), CanButtonBePressed);
		m_selectButton = new GateLogicalButton(PlayerInputLookup.GetAnyButton(PlayerInputLookup.LogicalButtonID.UISelectNotStart), CanButtonBePressed);
		m_dashButton = new GateLogicalButton(PlayerInputLookup.GetAnyButton(PlayerInputLookup.LogicalButtonID.Dash), CanButtonBePressed);
	}

	private PortalMapNode FindNodeForLevel(int _levelIndex)
	{
		PortalMapNode portalMapNode = null;
		PortalMapNode[] array = UnityEngine.Object.FindObjectsOfType<PortalMapNode>();
		portalMapNode = Array.Find(array, (PortalMapNode x) => x.LevelIndex == _levelIndex);
		if (portalMapNode == null)
		{
			MultiLevelMiniPortalMapNode[] array2 = UnityEngine.Object.FindObjectsOfType<MultiLevelMiniPortalMapNode>();
			portalMapNode = Array.Find(array2, (MultiLevelMiniPortalMapNode x) => x.AlternateLevelIndexes.Contains(_levelIndex));
		}
		return portalMapNode;
	}

	public void MoveToStartingLocation()
	{
		GameUtils.EnsureBootstrapSetup();
		int lastLevelEntered = GameUtils.GetGameSession().Progress.SaveData.LastLevelEntered;
		if (lastLevelEntered != -1)
		{
			PortalMapNode portalMapNode = FindNodeForLevel(lastLevelEntered);
			if (portalMapNode != null)
			{
				base.transform.position = m_groundCast.GetClosestPointOnGround(portalMapNode.transform.position);
			}
		}
		else if (m_avatarControls.m_bStartOnFirstNode)
		{
			PortalMapNode portalMapNode2 = FindNodeForLevel(0);
			if (portalMapNode2 != null)
			{
				base.transform.position = m_groundCast.GetClosestPointOnGround(portalMapNode2.transform.position);
			}
		}
		else
		{
			base.transform.position = m_groundCast.GetClosestPointOnGround(base.transform.position);
		}
	}

	public void RotateTowardsNextLevel()
	{
		GameProgress.GameProgressData saveData = GameUtils.GetGameSession().Progress.SaveData;
		int num = saveData.FarthestProgressedLevel(false);
		PortalMapNode portalMapNode = FindNodeForLevel((num != -1) ? num : 0);
		int lastLevelEntered = saveData.LastLevelEntered;
		PortalMapNode portalMapNode2 = FindNodeForLevel((lastLevelEntered != -1) ? lastLevelEntered : 0);
		if (portalMapNode != null && portalMapNode2 != null)
		{
			Vector3 closestPointOnGround = m_groundCast.GetClosestPointOnGround(portalMapNode.transform.position);
			Vector3 closestPointOnGround2 = m_groundCast.GetClosestPointOnGround(portalMapNode2.transform.position);
			Vector3 value = closestPointOnGround - closestPointOnGround2;
			if (value.sqrMagnitude > 0f)
			{
				value = Vector3.Normalize(value);
				base.gameObject.transform.rotation = Quaternion.LookRotation(value, base.transform.up);
			}
		}
	}

	public virtual void Awake()
	{
		m_rigidBody = base.gameObject.RequireComponent<Rigidbody>();
		if (m_avatarControls == null)
		{
			m_avatarControls = GetComponent<MapAvatarControls>();
		}
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_groundCast = base.gameObject.RequireComponent<MapAvatarGroundCast>();
		MoveToStartingLocation();
		InitControls();
		m_avatarControls = base.gameObject.RequireComponent<MapAvatarControls>();
		m_saveManager = GameUtils.RequireManager<SaveManager>();
		RotateTowardsNextLevel();
		Mailbox.Server.RegisterForMessageType(MessageType.MapAvatarHorn, OnMapAvatarHornMessage);
		UserSystemUtils.OnServerChangedGameState = (GenericVoid<GameState, GameStateMessage.GameStatePayload>)Delegate.Combine(UserSystemUtils.OnServerChangedGameState, new GenericVoid<GameState, GameStateMessage.GameStatePayload>(OnServerChangedGameState));
		base.StartSynchronising(synchronisedObject);
		if ((ConnectionStatus.IsHost() || !ConnectionStatus.IsInSession()) && DebugManager.Instance != null && DebugManager.Instance.GetOption("Auto Load Levels"))
		{
			StartCoroutine(m_avatarControls.DebugAutoLoadRandomLevel());
		}
	}

	public override void StopSynchronising()
	{
		UserSystemUtils.OnServerChangedGameState = (GenericVoid<GameState, GameStateMessage.GameStatePayload>)Delegate.Remove(UserSystemUtils.OnServerChangedGameState, new GenericVoid<GameState, GameStateMessage.GameStatePayload>(OnServerChangedGameState));
	}

	private void OnServerChangedGameState(GameState state, GameStateMessage.GameStatePayload payload)
	{
		if (state == GameState.InMap)
		{
			m_initialised = true;
		}
	}

	public override void OnDestroy()
	{
		Mailbox.Server.UnregisterForMessageType(MessageType.MapAvatarHorn, OnMapAvatarHornMessage);
		base.OnDestroy();
	}

	private void OnMapAvatarHornMessage(IOnlineMultiplayerSessionUserId userID, Serialisable message)
	{
		MapAvatarHornMessage mapAvatarHornMessage = (MapAvatarHornMessage)message;
		if (mapAvatarHornMessage.m_playerIdx < m_ServerData.m_bHorns.Length)
		{
			m_ServerData.m_bHorns[mapAvatarHornMessage.m_playerIdx] = true;
		}
		SendServerEvent(m_ServerData);
		for (int i = 0; i < m_ServerData.m_bHorns.Length; i++)
		{
			m_ServerData.m_bHorns[i] = false;
		}
	}

	protected override void OnEnable()
	{
		base.OnEnable();
		if (m_dashButton != null)
		{
			m_dashButton.ClaimPressEvent();
		}
		if (m_selectButton != null)
		{
			m_selectButton.ClaimPressEvent();
		}
	}

	protected override void OnDisable()
	{
		base.OnDisable();
		m_rigidBody.velocity = Vector3.zero;
	}

	private void Update()
	{
		if (m_initialised && m_avatarControls != null)
		{
			bool flag = m_avatarControls.enabled;
			bool flag2 = TimeManager.IsPaused(base.gameObject) || TimeManager.IsPaused(TimeManager.PauseLayer.Network) || T17DialogBoxManager.HasAnyOpenDialogs();
			if (flag && !flag2)
			{
				float deltaTime = TimeManager.GetDeltaTime(base.gameObject);
				Update_Movement(deltaTime);
				Update_Selection(deltaTime);
			}
			else
			{
				StopMovingAvatar();
			}
		}
	}

	private void FixedUpdate()
	{
		if (m_initialised && m_avatarControls != null && m_avatarControls.enabled)
		{
			m_avatarControls.UpdateMovement();
		}
	}

	private void StopMovingAvatar()
	{
		m_dashButton.ClaimPressEvent();
		m_selectButton.ClaimPressEvent();
		if (!m_rigidBody.IsSleeping())
		{
			m_rigidBody.velocity = new Vector3(0f, 0f, 0f);
		}
	}

	private float CalculateSpeed()
	{
		float num = 0f;
		if (m_moveX.GetValue() != 0f || m_moveY.GetValue() != 0f)
		{
			num = m_avatarControls.m_movementSpeed;
		}
		if (m_dashTimer > 0f)
		{
			float num2 = MathUtils.SinusoidalSCurve(m_dashTimer / m_avatarControls.m_dashTime);
			num = (1f - num2) * num + num2 * m_avatarControls.m_dashSpeed;
		}
		m_dashTimer -= TimeManager.GetDeltaTime(base.gameObject);
		return num;
	}

	private void Update_Movement(float _deltaTime)
	{
		if (m_dashButton != null && m_dashButton.JustPressed() && m_avatarControls != null && m_avatarControls.m_dashTime - m_dashTimer >= m_avatarControls.m_dashCooldown)
		{
			m_dashTimer = m_avatarControls.m_dashTime;
			m_ServerData.m_bDash = true;
			SendServerEvent(m_ServerData);
			m_ServerData.m_bDash = false;
		}
		float num = CalculateSpeed();
		PlayerControlsHelper.ControlAxisData _axisData = new PlayerControlsHelper.ControlAxisData
		{
			XAxisAllignment = PlayerControls.InversionType.Normal,
			YAxisAllignment = PlayerControls.InversionType.Normal,
			MoveX = m_moveX,
			MoveY = m_moveY,
			TurnSpeed = num / m_avatarControls.m_turningCircle,
			QuantiseDirection = false
		};
		PlayerControlsHelper.TurnTowardsControlAxis(ref _axisData, base.gameObject, _deltaTime);
		Vector3 forward = base.gameObject.transform.forward;
		Vector3 vector = num * forward;
		Vector3 groundNormal = m_groundCast.GetGroundNormal();
		Vector3 normalized = Vector3.Cross(groundNormal, Vector3.forward).normalized;
		Vector3 normalized2 = Vector3.Cross(normalized, groundNormal).normalized;
		Vector3 vector2 = vector;
		Vector3 _gameplayVelocity = normalized * vector2.x + groundNormal * vector2.y + normalized2 * vector2.z;
		ApplyGravity(ref _gameplayVelocity, m_rigidBody.velocity);
		m_rigidBody.velocity = _gameplayVelocity;
	}

	private float ApplyGravity(ref Vector3 _gameplayVelocity, Vector3 _physicalVelocity)
	{
		Vector3 vector = -m_groundCast.GetGroundNormal();
		float num = Vector3.Project(_physicalVelocity, vector).magnitude;
		if (!m_groundCast.HasGroundContact())
		{
			num += m_avatarControls.m_gravityStrength * TimeManager.GetDeltaTime(base.gameObject);
		}
		_gameplayVelocity += num * vector;
		return num;
	}

	private void Update_Selection(float _deltaTime)
	{
		IServerMapSelectable selectable = null;
		bool flag = m_avatarControls.CalculateCurrentSelectable<IServerMapSelectable>(m_avatarControls.GridManager, m_groundCast, out selectable);
		if (flag && m_selectButton.JustPressed() && selectable != null && !Application.isLoadingLevel && !m_saveManager.IsSaving)
		{
			selectable.OnSelected(m_avatarControls);
		}
		if (flag && m_currentSelectable != selectable)
		{
			if (m_currentSelectable != null)
			{
				m_currentSelectable.AvatarLeavingSelectable(m_avatarControls);
			}
			if (selectable != null)
			{
				selectable.AvatarEnteringSelectable(m_avatarControls);
			}
			m_currentSelectable = selectable;
			ServerSynchroniserBase serverSynchroniserBase = selectable as ServerSynchroniserBase;
			if (null != serverSynchroniserBase)
			{
				m_currentSelectableEntityID = serverSynchroniserBase.GetEntityId();
			}
			else
			{
				m_currentSelectableEntityID = 0u;
			}
			m_ServerData.CurrentSelectableEntityId = m_currentSelectableEntityID;
			SendServerEvent(m_ServerData);
		}
	}
}
