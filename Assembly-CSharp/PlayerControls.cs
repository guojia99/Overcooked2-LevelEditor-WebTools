using System;
using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

[ExecutionDependency(typeof(PlayerInputLookup))]
[AddComponentMenu("Scripts/Game/Player/PlayerControls")]
[RequireComponent(typeof(PlayerIDProvider))]
[RequireComponent(typeof(PlayerAttachmentCarrier))]
[RequireComponent(typeof(AttachmentThrower))]
[RequireComponent(typeof(GroundCast))]
[RequireComponent(typeof(RigidbodyMotion))]
public class PlayerControls : MonoBehaviour
{
	public enum InversionType
	{
		Normal = 0,
		Inverted = 1
	}

	[Serializable]
	public class MovementData
	{
		public float GravityStrength = 10f;

		public float GravityStrengthSlopedGround = 5f;

		public float RunSpeed = 4f;

		public float TurnSpeed = 20f;

		public float DashSpeed = 8f;

		public float DashTime = 1f;

		public float DashCooldown = 1f;

		public float FootstepLength = 0.2f;

		public ImpactData DashImpactData;

		public ImpactData ThrownImpactData;

		public ImpactData HazardImpactData;

		public float MaxSpeed = 12f;

		public float AutoSwitchTime = 0.5f;

		public float StepHeightMax = 0.65f;

		public InversionType XAxisAllignment;

		public InversionType YAxisAllignment;
	}

	[Serializable]
	public class ImpactData
	{
		public float Multiplier = 2f;

		public float Time = 0.2f;
	}

	public class ControlSchemeData
	{
		public ILogicalButton m_pickupButton;

		public ILogicalButton m_worksurfaceUseButton;

		public ILogicalButton m_dashButton;

		public ILogicalButton m_curseButton;

		public ILogicalValue m_moveX;

		public ILogicalValue m_moveY;

		private PlayerControls m_controls;

		public PlayerInputLookup.Player Player;

		private bool m_supressUse;

		public ControlSchemeData(PlayerInputLookup.Player _playerID, PlayerControls _controls)
		{
			m_controls = _controls;
			Player = _playerID;
			m_pickupButton = GetGated(PlayerInputLookup.GetButton(PlayerInputLookup.LogicalButtonID.PickupAndDrop, _playerID));
			m_worksurfaceUseButton = GetGated(PlayerInputLookup.GetButton(PlayerInputLookup.LogicalButtonID.WorkstationInteract, _playerID));
			m_dashButton = GetGated(PlayerInputLookup.GetButton(PlayerInputLookup.LogicalButtonID.Dash, _playerID));
			m_curseButton = GetGated(PlayerInputLookup.GetButton(PlayerInputLookup.LogicalButtonID.Curse, _playerID));
			m_moveX = GetGated(PlayerInputLookup.GetValue(PlayerInputLookup.LogicalValueID.MovementX, _playerID));
			m_moveY = GetGated(PlayerInputLookup.GetValue(PlayerInputLookup.LogicalValueID.MovementY, _playerID));
		}

		public bool IsUseDown()
		{
			return m_worksurfaceUseButton.IsDown() && !m_supressUse;
		}

		public bool IsUseSuppressed()
		{
			return m_supressUse;
		}

		public bool IsUseJustPressed()
		{
			return m_worksurfaceUseButton.JustPressed();
		}

		public bool IsUseJustReleased()
		{
			bool flag = m_worksurfaceUseButton.JustReleased();
			if (flag)
			{
				m_supressUse = false;
			}
			return flag;
		}

		public void ClearEvents()
		{
			m_pickupButton.ClaimPressEvent();
			m_pickupButton.ClaimReleaseEvent();
			m_worksurfaceUseButton.ClaimPressEvent();
			m_worksurfaceUseButton.ClaimReleaseEvent();
			if (m_worksurfaceUseButton.IsDown())
			{
				m_supressUse = true;
			}
			m_dashButton.ClaimPressEvent();
			m_dashButton.ClaimReleaseEvent();
			m_curseButton.ClaimPressEvent();
			m_curseButton.ClaimReleaseEvent();
		}

		private ILogicalButton GetGated(ILogicalButton _toProtect)
		{
			return new GateLogicalButton(_toProtect, m_controls.CanButtonBePressed);
		}

		private ILogicalValue GetGated(ILogicalValue _toProtect)
		{
			return new GateLogicalValue(_toProtect, m_controls.CanButtonBePressed);
		}
	}

	public class InteractionObjects
	{
		public IClientHandlePickup m_iHandlePickup;

		public ICatchable m_iHandleCatch;

		public IClientHandlePlacement m_iHandlePlacement;

		public ClientInteractable m_interactable;

		public IGridLocation m_gridLocation;

		public GameObject m_TheOriginalHandlePickup;

		public void Reset()
		{
			m_iHandlePickup = null;
			m_iHandleCatch = null;
			m_iHandlePlacement = null;
			m_interactable = null;
			m_gridLocation = null;
			m_TheOriginalHandlePickup = null;
		}

		public void Copy(InteractionObjects other)
		{
			m_iHandlePickup = other.m_iHandlePickup;
			m_iHandleCatch = other.m_iHandleCatch;
			m_iHandlePlacement = other.m_iHandlePlacement;
			m_interactable = other.m_interactable;
			m_gridLocation = other.m_gridLocation;
			m_TheOriginalHandlePickup = other.m_TheOriginalHandlePickup;
		}
	}

	[Serializable]
	public class ThrowIndicators
	{
		[Serializable]
		public struct ThrowIndicatorsCosmetics
		{
			[Header("Aiming Textures")]
			public Texture m_aimIndicator;

			public Texture m_aimIndicatorShadow;

			[Header("Idle Textures")]
			public Texture m_idleIndicator;

			public Texture m_idleIndicatorShadow;
		}

		[Header("Aiming Renderers")]
		[SerializeField]
		private MeshRenderer m_aimIndicator;

		[SerializeField]
		private MeshRenderer m_aimIndicatorShadow;

		[Header("Idle Renderers")]
		[SerializeField]
		private MeshRenderer m_idleIndicator;

		[SerializeField]
		private MeshRenderer m_idleIndicatorShadow;

		[Header("Cosmetics")]
		[SerializeField]
		private ThrowIndicatorsCosmetics[] m_throwingIndicatorCosmetics;

		private bool? m_isAiming;

		public void Show(bool _isAiming)
		{
			if (m_isAiming != _isAiming)
			{
				m_isAiming = _isAiming;
				m_aimIndicator.gameObject.SetActive(_isAiming);
				m_aimIndicatorShadow.gameObject.SetActive(_isAiming);
				m_idleIndicator.gameObject.SetActive(!_isAiming);
				m_idleIndicatorShadow.gameObject.SetActive(!_isAiming);
			}
		}

		public void UpdateCosmetics(TeamID _team, int _playerNum)
		{
			int num = _playerNum;
			if (_team != TeamID.None)
			{
				num = (int)(1 - _team);
			}
			if (num > -1 && num < 4)
			{
				ThrowIndicatorsCosmetics throwIndicatorsCosmetics = m_throwingIndicatorCosmetics[num];
				m_aimIndicator.material.SetTexture("_MainTex", throwIndicatorsCosmetics.m_aimIndicator);
				m_aimIndicatorShadow.material.SetTexture("_MainTex", throwIndicatorsCosmetics.m_aimIndicatorShadow);
				m_idleIndicator.material.SetTexture("_MainTex", throwIndicatorsCosmetics.m_idleIndicator);
				m_idleIndicatorShadow.material.SetTexture("_MainTex", throwIndicatorsCosmetics.m_idleIndicatorShadow);
			}
		}

		public void Hide()
		{
			m_aimIndicator.gameObject.SetActive(false);
			m_aimIndicatorShadow.gameObject.SetActive(false);
			m_idleIndicator.gameObject.SetActive(false);
			m_idleIndicatorShadow.gameObject.SetActive(false);
			m_isAiming = null;
		}
	}

	[SerializeField]
	private MovementData m_movement;

	[SerializeField]
	private ThrowIndicators m_throwIndicator;

	[SerializeField]
	private string m_chopTrigger;

	[SerializeField]
	public float m_timeBeforeFalling = 0.2f;

	[SerializeField]
	public float m_pickupDelay = 0.5f;

	[SerializeField]
	private LayerMask m_interactMask;

	[SerializeField]
	private LayerMask m_catchMask;

	[SerializeField]
	private LevelConfigBase m_debugLevelConfig;

	[SerializeField]
	private bool m_directlyUnderPlayerControl = true;

	[SerializeField]
	public float m_analogEnableDeadzoneThreshold = 0.25f;

	[SerializeField]
	public float m_analogEnableAngleThreshold = 15f;

	private SuppressionController m_directControlSuppression = new SuppressionController();

	public GameObject ImpactPFXPrefab;

	public GameObject DashPFXPrefab;

	public ParticleSystem CursePFXPrefab;

	public GameObject CatchPFXPrefab;

	[HideInInspector]
	public bool m_bRespawning;

	[HideInInspector]
	public bool m_bApplyGravity = true;

	private ControlSchemeData m_controlScheme;

	private PlayerIDProvider m_playerIDProvider;

	private GroundCast m_groundCast;

	private RigidbodyMotion m_motion;

	private LevelConfigBase m_levelConfigBase;

	private SurfaceMovable m_surfaceMovable;

	private WindAccumulator m_windReceiver;

	private PlayerPhysicsSurface m_currentPhysicsSurface;

	private ServerPlayerAttachmentCarrier m_serverCarrier;

	private ClientPlayerAttachmentCarrier m_clientCarrier;

	private PlayerControlsImpl_Default m_impl_default;

	private IPlayerControlsImpl m_activeControlsImpl;

	private float m_movementScale = 1f;

	private bool m_bServerControlled;

	private bool m_bInit;

	private const float c_InteractRadius = 1f;

	private const float c_InteractArc = (float)Math.PI;

	private const float c_CatchRadius = 2f;

	private const float c_CatchArc = (float)Math.PI / 2f;

	private const int c_NearbyObjectHitMax = 50;

	private Collider[] m_colliders = new Collider[50];

	private bool m_bGridSelection;

	private InteractionObjects m_interactionObjects = new InteractionObjects();

	private InteractionObjects m_tmpInteractionObjects = new InteractionObjects();

	private Transform m_Transform;

	private Transform m_previousParent;

	private Vector3 m_previousPosition = default(Vector3);

	private Vector3 m_localVelocity = default(Vector3);

	private float m_xzSpeed;

	private bool m_allowSwitchWhenDisabled;

	private InteractWithItemHelper.ScanCondition<ClientInteractable> m_CanInteractCondition;

	private InteractWithItemHelper.ScanCondition m_PickupOrPlaceCondition;

	private InteractWithItemHelper.ScanCondition<ServerCatchableItem> m_CatchCondition = delegate(ServerCatchableItem _catchable)
	{
		IThrowable component = ComponentCache<IThrowable>.GetComponent(_catchable.gameObject);
		IAttachment component2 = ComponentCache<IAttachment>.GetComponent(_catchable.gameObject);
		bool flag = component != null && component.IsFlying();
		bool flag2 = component2 != null && component2.IsAttached();
		return _catchable.enabled && !flag2 && flag;
	};

	private InteractWithItemHelper.ScanCondition<StaticGridLocation> m_GridLocationCondition = (StaticGridLocation _gridLocation) => _gridLocation.IsGridOccupant();

	public ControlSchemeData ControlScheme
	{
		get
		{
			return m_controlScheme;
		}
	}

	public PlayerIDProvider PlayerIDProvider
	{
		get
		{
			return m_playerIDProvider;
		}
	}

	public MovementData Movement
	{
		get
		{
			return m_movement;
		}
	}

	public float MovementScale
	{
		get
		{
			return m_movementScale;
		}
	}

	public RigidbodyMotion Motion
	{
		get
		{
			return m_motion;
		}
	}

	public InteractionObjects CurrentInteractionObjects
	{
		get
		{
			return m_interactionObjects;
		}
	}

	public LevelConfigBase LevelConfig
	{
		get
		{
			return m_levelConfigBase;
		}
	}

	public LayerMask GroundLayer
	{
		get
		{
			return (!m_groundCast.HasGroundContact()) ? default(LayerMask) : m_groundCast.GetGroundLayer();
		}
	}

	public Collider GroundCollider
	{
		get
		{
			return (!m_groundCast.HasGroundContact()) ? null : m_groundCast.GetGroundCollider();
		}
	}

	public Vector3 GroundNormal
	{
		get
		{
			return (!m_groundCast.HasGroundContact()) ? Vector3.up : m_groundCast.GetGroundNormal();
		}
	}

	public float GroundDistance
	{
		get
		{
			return m_groundCast.GetGroundDistance();
		}
	}

	public SurfaceMovable SurfaceMovable
	{
		get
		{
			return m_surfaceMovable;
		}
	}

	public WindAccumulator WindReceiver
	{
		get
		{
			return m_windReceiver;
		}
	}

	public PlayerPhysicsSurface PhysicsSurface
	{
		get
		{
			return m_currentPhysicsSurface;
		}
	}

	public ThrowIndicators ThrowIndicator
	{
		get
		{
			return m_throwIndicator;
		}
	}

	public bool ServerControlled
	{
		get
		{
			return m_bServerControlled;
		}
	}

	public bool AllowSwitchingWhenDisabled
	{
		get
		{
			return m_allowSwitchWhenDisabled;
		}
		set
		{
			m_allowSwitchWhenDisabled = value;
		}
	}

	private InteractWithItemHelper.ScanCondition<ClientInteractable> BuildCanInteractCondition(GameObject _object)
	{
		return (ClientInteractable _interact) => _interact.CanInteract(_object);
	}

	private InteractWithItemHelper.ScanCondition BuildPickupOrPlaceCondition(ICarrier _carrier)
	{
		return delegate(GameObject _object)
		{
			if (_carrier != null)
			{
				GameObject gameObject = _carrier.InspectCarriedItem();
				if (gameObject == null || !_object.IsInHierarchyOf(gameObject))
				{
					IClientHandlePickup controllingPickupHandler_Client = PlayerControlsHelper.GetControllingPickupHandler_Client(_object);
					IClientHandlePlacement controllingPlacementHandler_Client = PlayerControlsHelper.GetControllingPlacementHandler_Client(_object);
					bool flag = controllingPickupHandler_Client != null && controllingPickupHandler_Client.CanHandlePickup(_carrier);
					bool flag2 = gameObject != null && controllingPlacementHandler_Client != null;
					return flag || flag2;
				}
			}
			return false;
		};
	}

	public bool CanButtonBePressed()
	{
		if (!Application.isFocused)
		{
			return false;
		}
		if (!GetDirectlyUnderPlayerControl())
		{
			return false;
		}
		if (T17DialogBoxManager.HasAnyOpenDialogs() || (T17InGameFlow.Instance != null && T17InGameFlow.Instance.m_Rootmenu.GetCurrentOpenMenu() != null))
		{
			return false;
		}
		return true;
	}

	public bool GetDirectlyUnderPlayerControl()
	{
		return m_directlyUnderPlayerControl && !m_directControlSuppression.IsSuppressed();
	}

	public void SetDirectlyUnderPlayerControl(bool _underControl)
	{
		if (m_directlyUnderPlayerControl != _underControl)
		{
			m_directlyUnderPlayerControl = _underControl;
		}
	}

	public Suppressor Suppress(UnityEngine.Object _suppressor)
	{
		return m_directControlSuppression.AddSuppressor(_suppressor);
	}

	public void ReleaseSuppressor(Suppressor _suppressor)
	{
		_suppressor.Release();
		m_directControlSuppression.UpdateSuppressors();
	}

	public bool IsSuppressed()
	{
		return m_directControlSuppression.IsSuppressed();
	}

	public void SetMovementScale(float _scale)
	{
		m_movementScale = _scale;
	}

	public float GetUnclampedMovementSpeed()
	{
		return m_xzSpeed / m_movement.RunSpeed;
	}

	public float GetMovementSpeed()
	{
		return Mathf.Clamp01(GetUnclampedMovementSpeed());
	}

	public ClientInteractable GetCurrentlyInteracting()
	{
		return m_impl_default.GetCurrentlyInteracting();
	}

	public bool IsDashing()
	{
		return m_impl_default.m_clientImpl.IsDashing();
	}

	public void RegisterForInteractTrigger(VoidGeneric<ClientInteractable> _callback)
	{
		m_impl_default.RegisterForInteractTrigger(_callback);
	}

	public void UnregisterForInteractTrigger(VoidGeneric<ClientInteractable> _callback)
	{
		m_impl_default.UnregisterForInteractTrigger(_callback);
	}

	public void RegisterForThrowTrigger(VoidGeneric<GameObject> _callback)
	{
		m_impl_default.RegisterForThrowTrigger(_callback);
	}

	public void UnregisterForThrowTrigger(VoidGeneric<GameObject> _callback)
	{
		m_impl_default.UnregisterForThrowTrigger(_callback);
	}

	public void RegisterForFallingTrigger(VoidGeneric<bool> _callback)
	{
		m_impl_default.RegisterForFallingTrigger(_callback);
	}

	public void UnregisterForFallingTrigger(VoidGeneric<bool> _callback)
	{
		m_impl_default.UnregisterForFallingTrigger(_callback);
	}

	public void NotifySessionInteractionStarted(ClientSessionInteractable _interaction)
	{
		m_impl_default.NotifySessionInteractionStarted(_interaction);
	}

	public void NotifySessionInteractionEnded(ClientSessionInteractable _interaction)
	{
		m_impl_default.NotifySessionInteractionEnded(_interaction);
	}

	public void OnCollisionEnter(Collision _collision)
	{
		m_impl_default.OnCollisionEnter(_collision);
	}

	private void Awake()
	{
		m_playerIDProvider = base.gameObject.GetComponent<PlayerIDProvider>();
		m_groundCast = base.gameObject.RequireComponent<GroundCast>();
		m_groundCast.RegisterGroundChangedCallback(OnGroundChanged);
		m_motion = base.gameObject.RequireComponent<RigidbodyMotion>();
		m_surfaceMovable = base.gameObject.GetComponent<SurfaceMovable>();
		m_windReceiver = base.gameObject.GetComponent<WindAccumulator>();
		m_impl_default = base.gameObject.AddComponent<PlayerControlsImpl_Default>();
		m_CanInteractCondition = BuildCanInteractCondition(base.gameObject);
		m_Transform = base.transform;
		Mailbox.Client.RegisterForMessageType(MessageType.GameState, OnGameStateChanged);
	}

	private void Init()
	{
		if (!m_bInit)
		{
			m_impl_default.Init(this);
			m_bInit = true;
		}
	}

	private void Start()
	{
		SetActiveControlsImpl(m_impl_default);
		m_previousPosition = m_Transform.localPosition;
		m_previousParent = m_Transform.parent;
	}

	private void OnEnable()
	{
		if (m_activeControlsImpl != null)
		{
			m_activeControlsImpl.Enable();
		}
		m_previousPosition = m_Transform.localPosition;
		m_previousParent = m_Transform.parent;
	}

	private void OnDisable()
	{
		if (m_activeControlsImpl != null)
		{
			m_activeControlsImpl.Disable();
		}
		SetInteractionObjects(new InteractionObjects());
		m_xzSpeed = 0f;
	}

	private void OnDestroy()
	{
		Mailbox.Client.UnregisterForMessageType(MessageType.GameState, OnGameStateChanged);
		if (m_activeControlsImpl != null)
		{
			m_activeControlsImpl.Disable();
			m_activeControlsImpl = null;
		}
	}

	private void OnGameStateChanged(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
	{
		GameStateMessage gameStateMessage = (GameStateMessage)message;
		if (gameStateMessage.m_State == GameState.StartEntities)
		{
			m_clientCarrier = base.gameObject.RequestComponent<ClientPlayerAttachmentCarrier>();
			m_serverCarrier = base.gameObject.RequestComponent<ServerPlayerAttachmentCarrier>();
			if (m_clientCarrier != null)
			{
				m_PickupOrPlaceCondition = BuildPickupOrPlaceCondition(m_clientCarrier);
			}
			m_levelConfigBase = ((!(m_debugLevelConfig != null)) ? GameUtils.GetLevelConfig() : m_debugLevelConfig);
			m_bGridSelection = m_levelConfigBase.m_gridSelection;
			Init();
		}
	}

	private void Update()
	{
		if (!MultiplayerController.IsSynchronisationActive())
		{
			return;
		}
		if (!TimeManager.IsPaused(base.gameObject) && m_directControlSuppression.IsSuppressed())
		{
			m_directControlSuppression.UpdateSuppressors();
			if (m_controlScheme != null && !m_directControlSuppression.IsSuppressed())
			{
				m_controlScheme.ClearEvents();
			}
		}
		m_activeControlsImpl.Update_Impl();
	}

	public void FixedUpdate()
	{
		float fixedDeltaTime = TimeManager.GetFixedDeltaTime(base.gameObject);
		if (!(fixedDeltaTime > 0f))
		{
			return;
		}
		if (m_previousParent != m_Transform.parent)
		{
			Matrix4x4 matrix4x = Matrix4x4.identity;
			Matrix4x4 matrix4x2 = Matrix4x4.identity;
			if (m_Transform.parent != null)
			{
				matrix4x2 = m_Transform.parent.worldToLocalMatrix;
			}
			if (m_previousParent != null)
			{
				matrix4x = m_previousParent.worldToLocalMatrix.inverse;
			}
			m_previousPosition = matrix4x2 * matrix4x * new Vector4(m_previousPosition.x, m_previousPosition.y, m_previousPosition.z, 1f);
			m_previousParent = m_Transform.parent;
		}
		Vector3 localPosition = m_Transform.localPosition;
		m_localVelocity = localPosition - m_previousPosition;
		m_localVelocity /= fixedDeltaTime;
		m_previousPosition = localPosition;
		m_xzSpeed = m_localVelocity.XZ().magnitude;
	}

	public void UpdateNearbyObjects()
	{
		InteractionObjects interactionObjects = FindNearbyObjects();
		SetInteractionObjects(interactionObjects);
	}

	private void SetInteractionObjects(InteractionObjects _newInteractionObjects)
	{
		UpdateInteractAnticipation(InteractionType.Interact, m_interactionObjects.m_interactable, _newInteractionObjects.m_interactable);
		UpdateInteractAnticipation(InteractionType.Pickup, m_interactionObjects.m_iHandlePickup, _newInteractionObjects.m_iHandlePickup);
		UpdateInteractAnticipation(InteractionType.Placement, m_interactionObjects.m_iHandlePlacement, _newInteractionObjects.m_iHandlePlacement);
		UpdateInteractAnticipation(InteractionType.Catch, m_interactionObjects.m_iHandleCatch, _newInteractionObjects.m_iHandleCatch);
		UpdateInteractAnticipation(InteractionType.GridOccupant, m_interactionObjects.m_gridLocation, _newInteractionObjects.m_gridLocation);
		m_interactionObjects.Copy(_newInteractionObjects);
	}

	private IAnticipateInteractionNotifications[] GetAncipatorsFromComponent(object _participantCmpt)
	{
		if (_participantCmpt != null && _participantCmpt as MonoBehaviour != null)
		{
			GameObject obj = (_participantCmpt as MonoBehaviour).gameObject;
			return obj.RequestInterfaces<IAnticipateInteractionNotifications>();
		}
		return null;
	}

	private void UpdateInteractAnticipation(InteractionType _type, object _participantCmptBefore, object _participantCmptAfter)
	{
		if (_participantCmptBefore == _participantCmptAfter)
		{
			return;
		}
		IAnticipateInteractionNotifications[] ancipatorsFromComponent = GetAncipatorsFromComponent(_participantCmptBefore);
		IAnticipateInteractionNotifications[] ancipatorsFromComponent2 = GetAncipatorsFromComponent(_participantCmptAfter);
		if (ancipatorsFromComponent != null)
		{
			foreach (IAnticipateInteractionNotifications anticipateInteractionNotifications in ancipatorsFromComponent)
			{
				anticipateInteractionNotifications.OnInteractionAnticipationEnded(_type, base.gameObject);
			}
		}
		if (ancipatorsFromComponent2 != null)
		{
			foreach (IAnticipateInteractionNotifications anticipateInteractionNotifications2 in ancipatorsFromComponent2)
			{
				anticipateInteractionNotifications2.OnInteractionAnticipationStart(_type, base.gameObject);
			}
		}
	}

	private InteractionObjects FindNearbyObjects()
	{
		m_tmpInteractionObjects.Reset();
		Vector3 collidersInArc = InteractWithItemHelper.GetCollidersInArc(1f, (float)Math.PI, m_Transform, m_colliders, m_interactMask, m_bGridSelection);
		GameObject gameObject = ((!(m_clientCarrier != null)) ? null : m_clientCarrier.InspectCarriedItem());
		if ((bool)gameObject)
		{
			ClientUsableItem component = gameObject.GetComponent<ClientUsableItem>();
			if (component != null && component.CanInteract(base.gameObject))
			{
				m_tmpInteractionObjects.m_interactable = component;
			}
			else
			{
				m_tmpInteractionObjects.m_interactable = null;
			}
		}
		else
		{
			m_tmpInteractionObjects.m_interactable = InteractWithItemHelper.ScanForComponent(m_colliders, collidersInArc, m_CanInteractCondition);
		}
		GameObject gameObject2 = InteractWithItemHelper.ScanForObject(m_colliders, collidersInArc, m_PickupOrPlaceCondition);
		if (gameObject2 != null)
		{
			m_tmpInteractionObjects.m_TheOriginalHandlePickup = gameObject2;
			m_tmpInteractionObjects.m_iHandlePickup = PlayerControlsHelper.GetControllingPickupHandler_Client(gameObject2);
			m_tmpInteractionObjects.m_iHandlePlacement = PlayerControlsHelper.GetControllingPlacementHandler_Client(gameObject2);
		}
		else
		{
			m_tmpInteractionObjects.m_TheOriginalHandlePickup = null;
			m_tmpInteractionObjects.m_iHandlePickup = null;
			m_tmpInteractionObjects.m_iHandlePlacement = null;
		}
		m_tmpInteractionObjects.m_gridLocation = InteractWithItemHelper.ScanForComponent(m_colliders, collidersInArc, m_GridLocationCondition);
		return m_tmpInteractionObjects;
	}

	public ICatchable ScanForCatch()
	{
		Vector3 collidersInArc = InteractWithItemHelper.GetCollidersInArc(2f, (float)Math.PI / 2f, m_Transform, m_colliders, m_catchMask, false);
		return InteractWithItemHelper.ScanForComponent(m_colliders, collidersInArc, m_CatchCondition);
	}

	public IPlayerControlsImpl GetActiveControlsImpl()
	{
		return m_activeControlsImpl;
	}

	private void SetActiveControlsImpl(IPlayerControlsImpl _iImpl)
	{
		if (m_activeControlsImpl != null)
		{
			m_activeControlsImpl.Disable();
		}
		_iImpl.Enable();
		m_activeControlsImpl = _iImpl;
	}

	private void OnGroundChanged(Collider groundCollider)
	{
		m_currentPhysicsSurface = ((!(groundCollider != null)) ? null : groundCollider.gameObject.RequestComponent<PlayerPhysicsSurface>());
	}

	public void SetControlSchemeData(ControlSchemeData _controlScheme)
	{
		m_controlScheme = _controlScheme;
		m_impl_default.SetPlayerControlSchemeData(_controlScheme);
		if ((int)_controlScheme.Player >= ClientUserSystem.m_Users.Count)
		{
			return;
		}
		User user = null;
		int num = 0;
		for (int i = 0; i < ClientUserSystem.m_Users.Count; i++)
		{
			if (ClientUserSystem.m_Users._items[i].IsLocal)
			{
				if (num == (int)_controlScheme.Player)
				{
					user = ClientUserSystem.m_Users._items[i];
					break;
				}
				num++;
			}
		}
		if (user != null)
		{
			int playerNum = ClientUserSystem.m_Users._items.FindIndex_Predicate((User x) => x == user);
			m_throwIndicator.UpdateCosmetics(user.Team, playerNum);
		}
	}

	public void SetServerControlled(bool value)
	{
		m_bServerControlled = value;
	}
}
