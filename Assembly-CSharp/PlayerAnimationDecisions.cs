using GameModes.Horde;
using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

[RequireComponent(typeof(PlayerControls))]
[RequireComponent(typeof(PlayerIDProvider))]
[RequireComponent(typeof(HeldItemsMeshVisibility))]
[ExecutionDependency(typeof(ChefMeshReplacer))]
[AddComponentMenu("Scripts/Game/Player/PlayerAnimationDecisions")]
public class PlayerAnimationDecisions : AnimationInspectionBase
{
	private PlayerIDProvider m_playerIDProvider;

	private PlayerControls m_controls;

	private ClientPlayerAttachmentCarrier m_carrier;

	private ClientHeldItemsMeshVisibility m_heldItemsMeshVisibility;

	private Transform m_transform;

	private HeldItemsMeshVisibility.VisState m_visState = HeldItemsMeshVisibility.VisState.Idle;

	private bool m_chopping;

	private bool m_washingUp;

	private bool m_bStarted;

	private ParticleSystem.EmissionModule m_psEmission;

	private ClientTailMeshVisibility m_tailMeshVisibility;

	private TailMeshVisibility.VisState m_tailVisState = TailMeshVisibility.VisState.Visible;

	private ClientBodyMeshVisibility m_bodyMeshVisibility;

	private BodyMeshVisibility.VisState m_bodyVisState = BodyMeshVisibility.VisState.Visible;

	private static int m_iShowKnife = Animator.StringToHash("ShowKnife");

	private static int m_iWashingUp = Animator.StringToHash("WashingUp");

	private static int m_iThrow = Animator.StringToHash("Throw");

	private static int m_iChop = Animator.StringToHash("Chop");

	private static int m_iFalling = Animator.StringToHash("Falling");

	private static int m_Speed = Animator.StringToHash("Speed");

	private static int m_Holding = Animator.StringToHash("Holding");

	private static int m_Chopping = Animator.StringToHash("Chopping");

	private static int m_iPettingKevin = Animator.StringToHash("Pet");

	private static int m_bellows = Animator.StringToHash("Bellows");

	private static int m_bellowsUse = Animator.StringToHash("BellowsUse");

	private static int m_waterGun = Animator.StringToHash("WaterGun");

	[SerializeField]
	public float m_pushLerpSpeed = 15f;

	private static int m_iPushing = Animator.StringToHash("IsPushPull");

	private static int m_iPushX = Animator.StringToHash("PushPull_X");

	private static int m_iPushY = Animator.StringToHash("PushPull_Y");

	private Vector2 m_prevPushMovement = new Vector2(0f, 0f);

	private bool m_repairing;

	private static int m_iRepairing = Animator.StringToHash("Hammering");

	private bool m_inCannon;

	private static int m_iInCannon = Animator.StringToHash("isInCannon");

	private static int m_iCannonFired = Animator.StringToHash("CannonFired");

	private static int m_iCannonDuration = Animator.StringToHash("CannonDuration");

	private void Start()
	{
		Mailbox.Client.RegisterForMessageType(MessageType.GameState, OnGameStateChanged);
	}

	private void OnDestroy()
	{
		Mailbox.Client.UnregisterForMessageType(MessageType.GameState, OnGameStateChanged);
	}

	private void OnGameStateChanged(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
	{
		GameStateMessage gameStateMessage = (GameStateMessage)message;
		if (gameStateMessage.m_State == GameState.StartEntities)
		{
			m_transform = base.gameObject.transform;
			m_animator = base.gameObject.RequireComponentInImmediateChildren<Animator>();
			m_playerIDProvider = base.gameObject.GetComponent<PlayerIDProvider>();
			m_controls = base.gameObject.GetComponent<PlayerControls>();
			m_carrier = base.gameObject.GetComponent<ClientPlayerAttachmentCarrier>();
			m_heldItemsMeshVisibility = base.gameObject.GetComponent<ClientHeldItemsMeshVisibility>();
			m_heldItemsMeshVisibility.SetVisState(m_visState);
			m_tailMeshVisibility = base.gameObject.GetComponent<ClientTailMeshVisibility>();
			m_tailMeshVisibility.SetVisState(m_tailVisState);
			m_bodyMeshVisibility = base.gameObject.GetComponent<ClientBodyMeshVisibility>();
			m_bodyMeshVisibility.SetVisState(m_bodyVisState);
			m_controls.RegisterForInteractTrigger(OnInteract);
			m_controls.RegisterForThrowTrigger(OnThrow);
			m_controls.RegisterForFallingTrigger(OnFall);
			GameObject obj = base.gameObject.RequestChild("PFX_RunningPuff");
			ParticleSystem particleSystem = obj.RequestComponent<ParticleSystem>();
			m_psEmission = particleSystem.emission;
			AddDatastreams();
			m_bStarted = true;
		}
	}

	private void Update()
	{
		if (m_bStarted)
		{
			UpdateVariables();
			UpdateVisStates();
		}
	}

	private void OnInteract(ClientInteractable _interactable)
	{
		if ((bool)_interactable && _interactable.gameObject.GetComponent<BellowsSpray>() != null && m_animator != null && m_animator.isInitialized)
		{
			m_animator.SetTrigger(m_bellowsUse);
		}
	}

	private void OnThrow(GameObject _throwable)
	{
		if (_throwable != null && m_animator != null && m_animator.isInitialized)
		{
			m_animator.SetTrigger(m_iThrow);
		}
	}

	private void OnFall(bool _isFalling)
	{
		if (m_animator != null && m_animator.isInitialized)
		{
			m_animator.SetBool(m_iFalling, _isFalling);
		}
	}

	public bool IsHoldingSomething()
	{
		if (m_carrier != null)
		{
			return m_carrier.InspectCarriedItem() != null;
		}
		return false;
	}

	protected bool IsHolding<T>()
	{
		if (m_carrier != null)
		{
			GameObject gameObject = m_carrier.InspectCarriedItem();
			if (gameObject != null && gameObject.GetComponent<T>() != null)
			{
				return true;
			}
		}
		return false;
	}

	public bool IsHoldingWaterGun()
	{
		return IsHolding<WaterGunSpray>();
	}

	public bool IsHoldingBellows()
	{
		return IsHolding<BellowsSpray>();
	}

	protected bool IsInteracting<T>()
	{
		if (m_controls != null)
		{
			ClientInteractable currentlyInteracting = m_controls.GetCurrentlyInteracting();
			if (currentlyInteracting != null && currentlyInteracting.GetComponent<T>() != null)
			{
				return true;
			}
		}
		return false;
	}

	public bool IsChopping()
	{
		return IsInteracting<Workstation>();
	}

	public bool IsWashing()
	{
		return IsInteracting<WashingStation>();
	}

	public bool IsPettingKevin()
	{
		return IsInteracting<StoryKevinCosmeticDecisions>();
	}

	public bool IsPushingPushable()
	{
		return IsInteracting<PushableObject>();
	}

	public bool IsWearingBackpack()
	{
		if (m_carrier != null)
		{
			return m_carrier.InspectCarriedItem(PlayerAttachTarget.Back) != null;
		}
		return false;
	}

	public bool IsRepairing()
	{
		return IsInteracting<HordeTarget>();
	}

	public void SetInCannon(bool _inCannon)
	{
		m_inCannon = _inCannon;
	}

	public bool IsInCannon()
	{
		return m_inCannon;
	}

	public void FireCannon()
	{
		m_animator.SetTrigger(m_iCannonFired);
	}

	public void SetCannonSpeed(float speed)
	{
		m_animator.SetFloat(m_iCannonDuration, speed);
	}

	private void UpdateVariables()
	{
		if (m_animator == null || !m_animator.isInitialized)
		{
			return;
		}
		m_animator.SetFloat(m_Speed, m_controls.GetMovementSpeed());
		m_animator.SetBool(m_Holding, IsHoldingSomething());
		m_chopping = IsChopping();
		m_washingUp = IsWashing();
		m_animator.SetBool(m_Chopping, m_chopping);
		m_animator.SetBool(m_iWashingUp, m_washingUp);
		m_animator.SetBool(m_iPettingKevin, IsPettingKevin());
		m_animator.SetBool(m_waterGun, IsHoldingWaterGun());
		m_animator.SetBool(m_bellows, IsHoldingBellows());
		bool flag = IsPushingPushable() && m_controls.GetDirectlyUnderPlayerControl();
		Vector3 vector = Vector3.zero;
		if (flag)
		{
			ClientInteractable currentlyInteracting = m_controls.GetCurrentlyInteracting();
			if (currentlyInteracting != null && currentlyInteracting.gameObject != null)
			{
				ClientPushableObject clientPushableObject = currentlyInteracting.gameObject.RequireComponent<ClientPushableObject>();
				Vector2 cosmeticMovementDirection = clientPushableObject.CosmeticMovementDirection;
				vector = new Vector3(cosmeticMovementDirection.x, 0f, cosmeticMovementDirection.y);
			}
			if (vector.sqrMagnitude > 0.001f)
			{
				vector = m_transform.InverseTransformDirection(vector);
				vector.Normalize();
			}
			vector.y = vector.z;
			vector.z = 0f;
		}
		float deltaTime = TimeManager.GetDeltaTime(base.gameObject.layer);
		vector = Vector2.MoveTowards(m_prevPushMovement, vector, m_pushLerpSpeed * deltaTime);
		m_animator.SetFloat(m_iPushX, vector.x);
		m_animator.SetFloat(m_iPushY, vector.y);
		m_prevPushMovement = vector;
		m_animator.SetBool(m_iPushing, flag);
		m_repairing = IsRepairing();
		m_animator.SetBool(m_iRepairing, m_repairing);
		m_animator.SetBool(m_iInCannon, m_inCannon);
		bool flag2 = m_controls.Motion.GetVelocity().sqrMagnitude > 0.001f;
		if (!flag2 && m_psEmission.enabled)
		{
			m_psEmission.enabled = false;
		}
		else if (flag2 && !m_psEmission.enabled && !m_controls.m_bRespawning)
		{
			m_psEmission.enabled = true;
		}
	}

	private void UpdateVisStates()
	{
		HeldItemsMeshVisibility.VisState visState = m_visState;
		visState = ((!m_chopping && (!m_animator.isInitialized || !(m_animator.GetFloat(m_iShowKnife) > 0f))) ? ((!m_washingUp && (!m_animator.isInitialized || !m_animator.GetBool(m_iWashingUp))) ? ((m_repairing || (m_animator.isInitialized && m_animator.GetBool(m_iRepairing))) ? HeldItemsMeshVisibility.VisState.Repairing : (IsHoldingSomething() ? HeldItemsMeshVisibility.VisState.Carrying : HeldItemsMeshVisibility.VisState.Idle)) : HeldItemsMeshVisibility.VisState.Washing) : HeldItemsMeshVisibility.VisState.Chopping);
		if (visState != m_visState)
		{
			m_heldItemsMeshVisibility.SetVisState(visState);
			m_visState = visState;
		}
		TailMeshVisibility.VisState tailVisState = m_tailVisState;
		tailVisState = ((!IsWearingBackpack()) ? TailMeshVisibility.VisState.Visible : TailMeshVisibility.VisState.Hidden);
		if (tailVisState != m_tailVisState)
		{
			m_tailMeshVisibility.SetVisState(tailVisState);
			m_tailVisState = tailVisState;
		}
		BodyMeshVisibility.VisState bodyVisState = m_bodyVisState;
		bodyVisState = ((!IsInCannon()) ? BodyMeshVisibility.VisState.Visible : BodyMeshVisibility.VisState.Hidden);
		if (bodyVisState != m_bodyVisState)
		{
			m_bodyMeshVisibility.SetVisState(bodyVisState);
			m_bodyVisState = bodyVisState;
		}
	}

	private void AddDatastreams()
	{
	}
}
