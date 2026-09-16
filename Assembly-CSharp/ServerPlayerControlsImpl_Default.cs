using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerPlayerControlsImpl_Default : ServerSynchroniserBase, ITriggerReceiver
{
	private PlayerControls m_controls;

	private PlayerControls.ControlSchemeData m_controlScheme;

	private GameObject m_controlObject;

	private ServerInteractable m_lastInteracted;

	private PlayerSwitchingManager m_switchingManager;

	private ICarrier m_iCarrier;

	private IHandleCatch m_iCatcher;

	private IThrower m_iThrower;

	private float m_autoSwitchTimer = float.MinValue;

	private const string m_curseTrigger = "Curse";

	private uint m_entityID;

	private PlayerControlsImpl_Default m_playerControlsImpl_Default;

	private PlayerIDProvider m_playerIDProvider;

	private void Awake()
	{
		base.enabled = false;
		Mailbox.Server.RegisterForMessageType(MessageType.ChefEvent, OnChefEvent);
		m_playerIDProvider = GetComponent<PlayerIDProvider>();
	}

	public override void OnDestroy()
	{
		base.OnDestroy();
		Mailbox.Server.UnregisterForMessageType(MessageType.ChefEvent, OnChefEvent);
		if (m_iCarrier != null)
		{
			m_iCarrier.UnregisterCarriedItemChangeCallback(OnCarriedItemChanged);
		}
	}

	public override EntityType GetEntityType()
	{
		return EntityType.InputEvent;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(base.gameObject);
		m_entityID = entry.m_Header.m_uEntityID;
		m_playerControlsImpl_Default = (PlayerControlsImpl_Default)synchronisedObject;
		if (m_playerControlsImpl_Default != null)
		{
			m_playerControlsImpl_Default.m_serverImpl = this;
		}
	}

	public void Init(PlayerControls _controls)
	{
		m_controls = _controls;
		m_controlScheme = _controls.ControlScheme;
		m_controlObject = _controls.gameObject;
		m_iCarrier = _controls.gameObject.RequireInterface<ICarrier>();
		m_iCatcher = _controls.gameObject.RequireInterface<IHandleCatch>();
		m_iThrower = _controls.gameObject.RequireInterface<IThrower>();
		m_switchingManager = GameUtils.RequireManager<PlayerSwitchingManager>();
		m_switchingManager.AvatarSelectChangeCallback += OnAvatarSelectionChange;
		base.enabled = true;
		m_iCarrier.RegisterCarriedItemChangeCallback(OnCarriedItemChanged);
	}

	private void OnCarriedItemChanged(GameObject _before, GameObject _after)
	{
		if (m_lastInteracted != null && (bool)m_lastInteracted.GetComponent<UsableItem>())
		{
			EndInteraction();
		}
	}

	private void OnAvatarSelectionChange(PlayerInputLookup.Player _player, PlayerControls _controls)
	{
		if (m_controls == _controls)
		{
			m_autoSwitchTimer = float.MinValue;
			if (_controls != null)
			{
				_controls.ControlScheme.ClearEvents();
			}
		}
	}

	public void Enable()
	{
		base.enabled = true;
	}

	public void Disable()
	{
		EndInteraction();
		base.enabled = false;
	}

	public override void UpdateSynchronising()
	{
		base.UpdateSynchronising();
	}

	public void Update_Impl()
	{
		float deltaTime = TimeManager.GetDeltaTime(base.gameObject);
		Update_Carry(deltaTime);
		Update_Catch(deltaTime);
		Update_AutoSwitch(deltaTime, m_controlScheme);
		Update_Interacting();
	}

	public void SetPlayerControlSchemeData(PlayerControls.ControlSchemeData _controlScheme)
	{
		m_controlScheme = _controlScheme;
	}

	private void Update_AutoSwitch(float _deltaTime, PlayerControls.ControlSchemeData _controlSceme)
	{
		if (m_autoSwitchTimer > 0f)
		{
			m_autoSwitchTimer -= _deltaTime;
			if (m_autoSwitchTimer <= 0f)
			{
				m_switchingManager.ForceSwitchToNext(_controlSceme.Player);
			}
		}
	}

	private void Update_Carry(float _deltaTime)
	{
		if (m_iCarrier.InspectCarriedItem() != null && PlayerControlsHelper.WouldFallIfHoldingNothing(m_controls))
		{
			Vector2 directionXZ = base.transform.forward.XZ();
			PlayerControlsHelper.DropHeldItem(m_controls, directionXZ);
		}
	}

	public void ReceivePickUpEvent(GameObject _target)
	{
		if (_target != null && m_iCarrier.InspectCarriedItem() == null)
		{
			IHandlePickup controllingPickupHandler_Server = PlayerControlsHelper.GetControllingPickupHandler_Server(_target);
			if (controllingPickupHandler_Server != null && controllingPickupHandler_Server.CanHandlePickup(m_iCarrier))
			{
				Vector2 normalized = m_controls.transform.forward.XZ().normalized;
				controllingPickupHandler_Server.HandlePickup(m_iCarrier, normalized);
			}
		}
	}

	public void ReceivePlaceEvent(GameObject _target)
	{
		if (_target != null && m_iCarrier.InspectCarriedItem() != null)
		{
			PlayerControlsHelper.PlaceHeldItem_Server(m_controls, _target);
		}
	}

	public void ReceiveTakeEvent()
	{
		if (m_iCarrier.InspectCarriedItem() != null)
		{
			m_iCarrier.TakeItem();
		}
	}

	public void ReceiveInteractEvent(GameObject _target)
	{
		if (null != _target)
		{
			ServerInteractable component = _target.GetComponent<ServerInteractable>();
			bool flag = component != null && component.CanInteract(m_controlObject);
			bool flag2 = component != m_lastInteracted;
			if (flag)
			{
				if (flag2)
				{
					EndInteraction();
					BeginInteraction(component);
				}
			}
			else
			{
				EndInteraction();
			}
		}
		else
		{
			EndInteraction();
		}
	}

	public void ReceiveTriggerInteractEvent(GameObject _target)
	{
		ServerInteractable serverInteractable = null;
		if (_target != null)
		{
			serverInteractable = _target.RequestComponent<ServerInteractable>();
		}
		if (serverInteractable != null && serverInteractable.CanInteract(m_controlObject))
		{
			TriggerInteractable(serverInteractable);
		}
	}

	public void ReceiveThrowEvent(GameObject _target)
	{
		if (_target != null && !(m_iCarrier.InspectCarriedItem() == null) && !PlayerControlsHelper.IsHeldItemInsideStaticCollision(m_controls))
		{
			IThrowable throwable = _target.RequireInterface<IThrowable>();
			Vector3 vector = m_controlObject.transform.forward.normalized.XZ();
			if (throwable.CanHandleThrow(m_iThrower, vector))
			{
				m_iCarrier.TakeItem();
				throwable.HandleThrow(m_iThrower, vector);
				EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(_target);
				InputEventMessage inputEventMessage = new InputEventMessage(InputEventMessage.InputEventType.EndThrow);
				inputEventMessage.entityId = entry.m_Header.m_uEntityID;
				SendServerEvent(inputEventMessage);
			}
			else
			{
				m_iThrower.OnFailedToThrowItem(m_iCarrier.InspectCarriedItem());
			}
		}
	}

	private void Update_Catch(float _deltaTime)
	{
		if (m_controls.m_bRespawning)
		{
			return;
		}
		ICatchable catchable = m_controls.ScanForCatch();
		if (catchable == null)
		{
			return;
		}
		MonoBehaviour monoBehaviour = (MonoBehaviour)catchable;
		if (!(monoBehaviour == null) && !(monoBehaviour.gameObject == null))
		{
			Vector2 normalized = m_controlObject.transform.forward.XZ().normalized;
			if (m_iCatcher.CanHandleCatch(catchable, normalized))
			{
				m_iCatcher.HandleCatch(catchable, normalized);
				EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(monoBehaviour.gameObject);
				InputEventMessage inputEventMessage = new InputEventMessage(InputEventMessage.InputEventType.Catch);
				inputEventMessage.entityId = entry.m_Header.m_uEntityID;
				SendServerEvent(inputEventMessage);
			}
		}
	}

	public void OnTrigger(string _trigger)
	{
		if (_trigger == "Curse")
		{
			InputEventMessage message = new InputEventMessage(InputEventMessage.InputEventType.Curse);
			SendServerEvent(message);
		}
	}

	private void Update_Interacting()
	{
		if (null != m_lastInteracted && (!m_lastInteracted.CanInteract(base.gameObject) || m_controls.m_bRespawning))
		{
			EndInteraction();
		}
	}

	private void TriggerInteractable(ServerInteractable _interactable)
	{
		InputEventMessage inputEventMessage = new InputEventMessage(InputEventMessage.InputEventType.TriggerInteraction);
		if (_interactable != null)
		{
			EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(_interactable.gameObject);
			uint uEntityID = entry.m_Header.m_uEntityID;
			inputEventMessage.entityId = uEntityID;
		}
		else
		{
			inputEventMessage.entityId = 0u;
		}
		SendServerEvent(inputEventMessage);
		if (_interactable != null)
		{
			_interactable.TriggerInteract(m_controlObject, m_controlObject.transform.forward.XZ().normalized);
		}
	}

	public void StartDash()
	{
		InputEventMessage message = new InputEventMessage(InputEventMessage.InputEventType.Dash);
		SendServerEvent(message);
	}

	public void StartDashCollision(Vector3 collisionPoint)
	{
		InputEventMessage inputEventMessage = new InputEventMessage(InputEventMessage.InputEventType.DashCollision);
		inputEventMessage.collisionContactPoint = collisionPoint;
		SendServerEvent(inputEventMessage);
	}

	private void OnChefEvent(IOnlineMultiplayerSessionUserId userID, Serialisable serialisable)
	{
		ChefEventMessage chefEventMessage = (ChefEventMessage)serialisable;
		if (m_entityID != chefEventMessage.ChefEntityID)
		{
			return;
		}
		EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(chefEventMessage.EntityID);
		EntitySerialisationEntry entry2 = EntitySerialisationRegistry.GetEntry(chefEventMessage.ChefEntityID);
		if (entry2 != null)
		{
			GameObject target = null;
			if (entry != null)
			{
				entry.SetRequiresUrgentUpdate(true);
				target = entry.m_GameObject;
			}
			switch (chefEventMessage.EventType)
			{
			case ChefEventMessage.ChefEventType.PickUp:
				ReceivePickUpEvent(target);
				break;
			case ChefEventMessage.ChefEventType.Place:
				ReceivePlaceEvent(target);
				break;
			case ChefEventMessage.ChefEventType.Take:
				ReceiveTakeEvent();
				break;
			case ChefEventMessage.ChefEventType.Interact:
				ReceiveInteractEvent(target);
				break;
			case ChefEventMessage.ChefEventType.TriggerInteract:
				ReceiveTriggerInteractEvent(target);
				break;
			case ChefEventMessage.ChefEventType.Throw:
				ReceiveThrowEvent(target);
				break;
			case ChefEventMessage.ChefEventType.KnockBack:
				ReceiveKnockbackEvent(chefEventMessage.Knockback_Type, chefEventMessage.KnockbackForce, chefEventMessage.RelativeContactPoint);
				break;
			}
		}
	}

	private void ReceiveKnockbackEvent(ChefEventMessage.KnockbackType _knockbackType, Vector2 _knockbackForce, Vector3 _relativeCollisionPoint)
	{
		m_playerControlsImpl_Default.m_clientImpl.DoKnockback(_knockbackType, _knockbackForce, _relativeCollisionPoint);
		ServerMessenger.SendChefEffectMessage(m_entityID, ChefEffectMessage.EffectType.Impact, _relativeCollisionPoint);
	}

	private void BeginInteraction(ServerInteractable _interactable)
	{
		InputEventMessage inputEventMessage = new InputEventMessage(InputEventMessage.InputEventType.BeginInteraction);
		if (_interactable != null)
		{
			EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(_interactable.gameObject);
			uint uEntityID = entry.m_Header.m_uEntityID;
			inputEventMessage.entityId = uEntityID;
		}
		else
		{
			inputEventMessage.entityId = 0u;
		}
		SendServerEvent(inputEventMessage);
		if (_interactable != null)
		{
			_interactable.BeginInteract(m_controlObject, m_controlObject.transform.forward.XZ().normalized);
		}
		m_lastInteracted = _interactable;
	}

	private void EndInteraction()
	{
		InputEventMessage message = new InputEventMessage(InputEventMessage.InputEventType.EndInteraction);
		SendServerEvent(message);
		if (m_lastInteracted != null)
		{
			m_lastInteracted.EndInteract(m_controlObject);
			m_lastInteracted = null;
		}
	}
}
