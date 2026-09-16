using System;
using System.Collections.Generic;
using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientPlayerControlsImpl_Default : ClientSynchroniserBase
{
	private PlayerControlsImpl_Default m_controlsImpl;

	private ClientInteractable m_lastInteracted;

	private ClientInteractable m_predictedInteracted;

	private Transform m_Transform;

	private ClientInteractable m_sessionInteraction;

	private PlayerControls m_controls;

	private PlayerControls.ControlSchemeData m_controlScheme;

	private GameObject m_controlObject;

	private CollisionRecorder m_collisionRecorder;

	private PlayerIDProvider m_controlsPlayer;

	private ICarrier m_iCarrier;

	private IClientThrower m_iThrower;

	private IClientHandleCatch m_iCatcher;

	private Vector3 m_lastVelocity;

	private float m_dashTimer = float.MinValue;

	private float m_attemptedDistanceCounter;

	private bool m_aimingThrow;

	private bool m_movementInputSuppressed;

	private Vector3 m_lastMoveInputDirection;

	private float m_impactStartTime = float.MaxValue;

	private float m_impactTimer = float.MinValue;

	private Vector3 m_impactVelocity = Vector3.zero;

	private float m_timeOffGround;

	private bool m_isFalling;

	private VoidGeneric<ClientInteractable> m_interactTriggerCallback = delegate
	{
	};

	private VoidGeneric<GameObject> m_throwTriggerCallback = delegate
	{
	};

	private VoidGeneric<bool> m_fallingTriggerCallback = delegate
	{
	};

	private PlayerControlsHelper.ControlAxisData m_controlAxisData = default(PlayerControlsHelper.ControlAxisData);

	private PlayerIDProvider m_playerIDProvider;

	private const float sixtyFPS = 1f / 60f;

	private float m_LeftOverTime;

	private OrderedMessageReceivedCallback m_onChefEffectReceived;

	private uint m_entityID;

	private float m_lastPickupTimestamp;

	private LayerMask m_SlopedGroundMask = 0;

	private void Awake()
	{
		base.enabled = false;
		m_playerIDProvider = base.gameObject.RequireComponent<PlayerIDProvider>();
		m_onChefEffectReceived = OnChefEffectReceived;
		m_Transform = base.transform;
		m_SlopedGroundMask = LayerMask.GetMask("SlopedGround");
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(base.gameObject);
		m_entityID = entry.m_Header.m_uEntityID;
		m_controlsImpl = (PlayerControlsImpl_Default)synchronisedObject;
		if (m_controlsImpl != null)
		{
			m_controlsImpl.m_clientImpl = this;
		}
		Mailbox.Client.RegisterForMessageType(MessageType.ChefEffect, m_onChefEffectReceived);
	}

	public override EntityType GetEntityType()
	{
		return EntityType.InputEvent;
	}

	private void OnChefEffectReceived(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
	{
		ChefEffectMessage chefEffectMessage = (ChefEffectMessage)message;
		if (chefEffectMessage.ChefEntityID == m_entityID)
		{
			switch (chefEffectMessage.Effect)
			{
			case ChefEffectMessage.EffectType.Impact:
				DoImpactEffect(chefEffectMessage.RelativePosition);
				break;
			case ChefEffectMessage.EffectType.Dash:
				DoDashCollisionEffects(m_playerIDProvider, chefEffectMessage.RelativePosition);
				break;
			}
		}
	}

	public void RegisterForInteractTrigger(VoidGeneric<ClientInteractable> _callback)
	{
		m_interactTriggerCallback = (VoidGeneric<ClientInteractable>)Delegate.Combine(m_interactTriggerCallback, _callback);
	}

	public void UnregisterForInteractTrigger(VoidGeneric<ClientInteractable> _callback)
	{
		m_interactTriggerCallback = (VoidGeneric<ClientInteractable>)Delegate.Remove(m_interactTriggerCallback, _callback);
	}

	public void RegisterForThrowTrigger(VoidGeneric<GameObject> _callback)
	{
		m_throwTriggerCallback = (VoidGeneric<GameObject>)Delegate.Combine(m_throwTriggerCallback, _callback);
	}

	public void UnregisterForThrowTrigger(VoidGeneric<GameObject> _callback)
	{
		m_throwTriggerCallback = (VoidGeneric<GameObject>)Delegate.Remove(m_throwTriggerCallback, _callback);
	}

	public void RegisterForFallingTrigger(VoidGeneric<bool> _callback)
	{
		m_fallingTriggerCallback = (VoidGeneric<bool>)Delegate.Combine(m_fallingTriggerCallback, _callback);
	}

	public void UnregisterForFallingTrigger(VoidGeneric<bool> _callback)
	{
		m_fallingTriggerCallback = (VoidGeneric<bool>)Delegate.Remove(m_fallingTriggerCallback, _callback);
	}

	public void Init(PlayerControls _controls)
	{
		m_controls = _controls;
		m_controlScheme = _controls.ControlScheme;
		m_controlObject = _controls.gameObject;
		m_collisionRecorder = _controls.gameObject.RequireComponent<CollisionRecorder>();
		m_controlsPlayer = _controls.gameObject.RequireComponent<PlayerIDProvider>();
		m_iCarrier = _controls.gameObject.RequireInterface<ICarrier>();
		m_iThrower = _controls.gameObject.RequireInterface<IClientThrower>();
		m_iCatcher = _controls.gameObject.RequireInterface<IClientHandleCatch>();
		if (T17InGameFlow.Instance != null)
		{
			T17InGameFlow.Instance.RegisterOnPauseMenuVisibilityChanged(OnPauseMenuVisibilityChange);
		}
	}

	public void Enable()
	{
		m_iCarrier.RegisterCarriedItemChangeCallback(OnCarriedItemChanged);
		base.enabled = true;
	}

	public void Disable()
	{
		m_iCarrier.UnregisterCarriedItemChangeCallback(OnCarriedItemChanged);
		m_lastVelocity = Vector3.zero;
		m_dashTimer = float.MinValue;
		EndInteraction();
		base.enabled = false;
		GetComponent<Rigidbody>().velocity = Vector3.zero;
	}

	public void SetPlayerControlSchemeData(PlayerControls.ControlSchemeData _controlScheme)
	{
		m_controlScheme = _controlScheme;
	}

	public override void UpdateSynchronising()
	{
		base.UpdateSynchronising();
	}

	public void Update_Impl()
	{
		float deltaTime = TimeManager.GetDeltaTime(base.gameObject);
		bool flag = m_playerIDProvider.IsLocallyControlled();
		if (TimeManager.IsPaused(TimeManager.PauseLayer.Network) && flag)
		{
			if (m_controlScheme != null)
			{
				Update_Movement(deltaTime, true);
			}
		}
		else
		{
			if (TimeManager.IsPaused(TimeManager.PauseLayer.Main) && flag)
			{
				return;
			}
			if (m_controlScheme != null)
			{
				bool isSuppressed = m_controlScheme.IsUseSuppressed();
				bool isUsePressed = m_controlScheme.IsUseDown();
				bool justPressed = m_controlScheme.IsUseJustPressed();
				bool justReleased = m_controlScheme.IsUseJustReleased();
				if (flag)
				{
					m_controls.UpdateNearbyObjects();
					Update_Carry();
					Update_Interact(deltaTime, isUsePressed, justPressed);
					Update_Throw(deltaTime, isUsePressed, justReleased, isSuppressed);
				}
				Update_Aim(deltaTime, isUsePressed);
				Update_Movement(deltaTime, false);
			}
			Update_Falling(deltaTime);
		}
	}

	private void Update_Carry()
	{
		if (!m_controls.ControlScheme.m_pickupButton.JustPressed())
		{
			return;
		}
		IClientHandlePickup iHandlePickup = m_controls.CurrentInteractionObjects.m_iHandlePickup;
		GameObject gameObject = m_iCarrier.InspectCarriedItem();
		if (gameObject != null)
		{
			PlayerControlsHelper.PlaceHeldItem_Client(m_controls);
		}
		else if (iHandlePickup != null && iHandlePickup.CanHandlePickup(m_iCarrier))
		{
			float num = ClientTime.Time();
			if (num >= m_lastPickupTimestamp)
			{
				ClientMessenger.ChefEventMessage(ChefEventMessage.ChefEventType.PickUp, base.gameObject, m_controls.CurrentInteractionObjects.m_TheOriginalHandlePickup);
				m_lastPickupTimestamp = num + m_controls.m_pickupDelay;
			}
		}
		else if (m_controls.CurrentInteractionObjects.m_interactable != null && m_controls.CurrentInteractionObjects.m_interactable.UsePlacementButton)
		{
			ClientMessenger.ChefEventMessage(ChefEventMessage.ChefEventType.TriggerInteract, base.gameObject, m_controls.CurrentInteractionObjects.m_interactable);
		}
	}

	private void Update_Interact(float _deltaTime, bool isUsePressed, bool justPressed)
	{
		ClientInteractable clientInteractable = null;
		if (m_controls.CurrentInteractionObjects.m_interactable != null)
		{
			clientInteractable = m_controls.CurrentInteractionObjects.m_interactable;
		}
		ClientInteractable clientInteractable2 = null;
		clientInteractable2 = ((isUsePressed && null != clientInteractable && null == m_predictedInteracted) ? clientInteractable : ((null == clientInteractable && null != m_predictedInteracted && m_predictedInteracted.InteractionIsSticky()) ? null : ((!isUsePressed && null != m_predictedInteracted && !m_predictedInteracted.InteractionIsSticky()) ? null : ((!(null != clientInteractable) || !(clientInteractable != m_predictedInteracted)) ? m_predictedInteracted : null))));
		if (clientInteractable2 != m_predictedInteracted)
		{
			m_predictedInteracted = clientInteractable2;
			ClientMessenger.ChefEventMessage(ChefEventMessage.ChefEventType.Interact, base.gameObject, clientInteractable2);
		}
		if (justPressed && clientInteractable != null)
		{
			ClientMessenger.ChefEventMessage(ChefEventMessage.ChefEventType.TriggerInteract, base.gameObject, clientInteractable);
		}
	}

	private void Update_Throw(float _deltaTime, bool isUsePressed, bool justReleased, bool isSuppressed)
	{
		IClientThrowable clientThrowable = null;
		if (m_iCarrier.InspectCarriedItem() != null)
		{
			clientThrowable = m_iCarrier.InspectCarriedItem().RequestInterface<IClientThrowable>();
		}
		if (!isSuppressed && justReleased && clientThrowable != null)
		{
			Vector2 normalized = m_controlObject.transform.forward.XZ().normalized;
			if (clientThrowable.CanHandleThrow(m_iThrower, normalized))
			{
				ClientMessenger.ChefEventMessage(ChefEventMessage.ChefEventType.Throw, base.gameObject, m_iCarrier.InspectCarriedItem());
			}
		}
	}

	private void Update_Aim(float _deltaTime, bool isUsePressed)
	{
		bool aimingThrow = m_aimingThrow;
		IClientThrowable clientThrowable = null;
		if (m_iCarrier.InspectCarriedItem() != null)
		{
			clientThrowable = m_iCarrier.InspectCarriedItem().RequestInterface<IClientThrowable>();
		}
		if (clientThrowable as MonoBehaviour != null)
		{
			Vector2 normalized = m_controlObject.transform.forward.XZ().normalized;
			m_aimingThrow = isUsePressed;
			m_aimingThrow &= clientThrowable != null && clientThrowable.CanHandleThrow(m_iThrower, normalized);
		}
		else
		{
			m_aimingThrow = false;
		}
		PlayerInputLookup.Player iD = m_playerIDProvider.GetID();
		bool flag = m_playerIDProvider.IsLocallyControlled();
		bool directlyUnderPlayerControl = m_controls.GetDirectlyUnderPlayerControl();
		if (flag && directlyUnderPlayerControl)
		{
			if (m_aimingThrow)
			{
				m_controls.ThrowIndicator.Show(true);
			}
			else
			{
				m_controls.ThrowIndicator.Show(false);
			}
		}
		else
		{
			m_controls.ThrowIndicator.Hide();
		}
	}

	private void Update_MovementSuppression(float _deltaTime, Vector3 _targetDirection)
	{
		float num = m_controls.m_analogEnableDeadzoneThreshold * m_controls.m_analogEnableDeadzoneThreshold;
		if (m_aimingThrow || m_sessionInteraction != null)
		{
			m_movementInputSuppressed = true;
			if (_targetDirection.sqrMagnitude >= num)
			{
				m_lastMoveInputDirection = PlayerControlsHelper.GetControlAxis(m_controls, ref m_controlAxisData);
			}
		}
		else
		{
			if (!m_movementInputSuppressed)
			{
				return;
			}
			if (_targetDirection.sqrMagnitude < num)
			{
				m_movementInputSuppressed = false;
				return;
			}
			Vector3 lhs = _targetDirection.SafeNormalised(Vector3.zero);
			Vector3 rhs = m_lastMoveInputDirection.SafeNormalised(Vector3.zero);
			float f = Vector3.Dot(lhs, rhs);
			float num2 = Mathf.Acos(f) * 57.29578f;
			if (num2 >= m_controls.m_analogEnableAngleThreshold)
			{
				m_movementInputSuppressed = false;
			}
		}
	}

	private void Update_Rotation(float _deltaTime, float xAxis, float yAxis, PlayerControls.InversionType xAxisAllignment, PlayerControls.InversionType yAxisAllignment)
	{
		if (m_controls.GetDirectlyUnderPlayerControl())
		{
			PlayerControlsHelper.TurnTowardsControlAxis(xAxis, yAxis, xAxisAllignment, yAxisAllignment, m_controlAxisData.TurnSpeed, m_controlObject, _deltaTime);
			return;
		}
		GameObject gameObject = null;
		ClientAttachmentCatcher clientAttachmentCatcher = m_iCatcher as ClientAttachmentCatcher;
		if (clientAttachmentCatcher != null)
		{
			gameObject = clientAttachmentCatcher.GetTrackedThrowable();
		}
		if (gameObject != null)
		{
			Vector3 direction = (gameObject.transform.position - m_Transform.position).SafeNormalised(m_Transform.forward);
			PlayerControlsHelper.TurnTowardsDirection(m_controlObject, direction, m_controls.Movement.TurnSpeed, _deltaTime);
		}
	}

	private void Update_Movement(float _deltaTime, bool _netPaused)
	{
		m_LeftOverTime += _deltaTime;
		_deltaTime = 1f / 60f;
		Vector3 vector = Vector3.zero;
		PlayerControlsHelper.BuildControlAxisData(m_controls, ref m_controlAxisData);
		float value = m_controlAxisData.MoveX.GetValue();
		float value2 = m_controlAxisData.MoveY.GetValue();
		if (!_netPaused)
		{
			vector = PlayerControlsHelper.GetControlAxis(value, value2, m_controlAxisData.XAxisAllignment, m_controlAxisData.YAxisAllignment);
		}
		while (m_LeftOverTime >= 1f / 60f)
		{
			m_LeftOverTime -= 1f / 60f;
			Update_Rotation(_deltaTime, value, value2, m_controlAxisData.XAxisAllignment, m_controlAxisData.YAxisAllignment);
			Update_MovementSuppression(_deltaTime, vector);
			ApplyGravityForce();
			ApplyWindForce(_deltaTime);
			PlayerControls.MovementData movement = m_controls.Movement;
			float movementScale = m_controls.MovementScale;
			Vector3 vector2 = movementScale * vector * movement.RunSpeed;
			if (m_movementInputSuppressed)
			{
				vector2 = Vector3.zero;
			}
			if (m_dashTimer > 0f)
			{
				Vector3 vector3 = movementScale * m_controlObject.transform.forward * movement.DashSpeed;
				float num = MathUtils.SinusoidalSCurve(m_dashTimer / movement.DashTime);
				vector2 = (1f - num) * vector2 + num * vector3;
			}
			m_dashTimer -= _deltaTime;
			if (m_impactTimer > 0f)
			{
				float num2 = MathUtils.SinusoidalSCurve(m_impactTimer / m_impactStartTime);
				vector2 = (1f - num2) * vector2 + num2 * m_impactVelocity;
			}
			m_impactTimer -= _deltaTime;
			vector2 = Vector3.ClampMagnitude(vector2, movement.MaxSpeed);
			Vector3 vector4 = ProgressVelocityWrtFriction(m_lastVelocity, m_controls.Motion.GetVelocity(), vector2, GetSurfaceData());
			m_controls.Motion.SetVelocity(vector4);
			m_lastVelocity = vector4;
			ApplyGroundMovement(_deltaTime);
			if (!_netPaused && m_controlScheme.m_dashButton.JustPressed() && movement.DashTime - m_dashTimer >= movement.DashCooldown && m_impactTimer < 0f)
			{
				m_dashTimer = movement.DashTime;
				if (m_controlsImpl.m_serverImpl != null)
				{
					m_controlsImpl.m_serverImpl.StartDash();
				}
				else
				{
					DoDash();
				}
				List<Collision> recentCollisions = m_collisionRecorder.GetRecentCollisions();
				for (int i = 0; i < recentCollisions.Count; i++)
				{
					Collision collision = recentCollisions[i];
					PlayerControls playerControls = collision.gameObject.RequestComponent<PlayerControls>();
					if (playerControls != null)
					{
						Vector3 relativeVelocity = collision.relativeVelocity + movementScale * m_controlObject.transform.forward * movement.DashSpeed;
						OnDashCollision(playerControls, collision.contacts[0].point, relativeVelocity);
					}
				}
			}
			m_attemptedDistanceCounter += vector2.magnitude * _deltaTime;
			if (m_attemptedDistanceCounter >= movement.FootstepLength)
			{
				m_attemptedDistanceCounter %= movement.FootstepLength;
			}
		}
	}

	private void Update_Falling(float _deltaTime)
	{
		if (m_controls.GroundCollider == null)
		{
			m_timeOffGround += _deltaTime;
			if (!m_isFalling && m_timeOffGround > m_controls.m_timeBeforeFalling)
			{
				m_fallingTriggerCallback(true);
				m_isFalling = true;
			}
		}
		else
		{
			if (m_isFalling)
			{
				m_isFalling = false;
				m_fallingTriggerCallback(false);
			}
			m_timeOffGround = 0f;
		}
	}

	public bool IsDashing()
	{
		return m_dashTimer > 0f;
	}

	public void OnCollisionEnter(Collision _collision)
	{
		GameObject obj = _collision.gameObject;
		if (m_dashTimer > 0f)
		{
			PlayerControls playerControls = obj.RequestComponent<PlayerControls>();
			if (playerControls != null)
			{
				OnDashCollision(playerControls, _collision.contacts[0].point, _collision.relativeVelocity);
			}
		}
		if (m_playerIDProvider.IsLocallyControlled())
		{
			IClientThrowable clientThrowable = obj.RequestInterfaceInImmediateChildren<IClientThrowable>();
			if (clientThrowable != null)
			{
				ContactPoint contactPoint = _collision.contacts[0];
				OnThrowableCollision(clientThrowable, _collision.collider, contactPoint.point, contactPoint.normal, _collision.relativeVelocity);
			}
			FireHazard fireHazard = obj.RequestInterface<FireHazard>();
			if (fireHazard != null)
			{
				ContactPoint contactPoint2 = _collision.contacts[0];
				OnFireHazardCollision(contactPoint2.point, _collision.contacts[0].normal, _collision.relativeVelocity);
			}
		}
		Steppable steppable = obj.RequestComponent<Steppable>();
		if (steppable != null)
		{
			ContactPoint contactPoint3 = _collision.contacts[0];
			OnStepCollision(steppable, contactPoint3.point, contactPoint3.normal, _collision.relativeVelocity);
		}
	}

	private void OnDashCollision(PlayerControls _otherPlayer, Vector3 _contactPoint, Vector3 _relativeVelocity)
	{
		if (m_dashTimer > 0f)
		{
			GameObject gameObject = _otherPlayer.gameObject;
			Transform transform = gameObject.transform;
			if (m_controlsImpl.m_serverImpl != null)
			{
				m_controlsImpl.m_serverImpl.StartDashCollision(_contactPoint);
				ServerMessenger.SendChefEffectMessage(gameObject, ChefEffectMessage.EffectType.Dash, _contactPoint);
			}
			Vector3 normalized = (transform.position - m_Transform.position).normalized;
			float magnitude = _relativeVelocity.magnitude;
			ClientPlayerControlsImpl_Default clientImpl = (_otherPlayer.GetActiveControlsImpl() as PlayerControlsImpl_Default).m_clientImpl;
			if (clientImpl != null && clientImpl.m_dashTimer > 0f)
			{
				m_dashTimer = float.MinValue;
				clientImpl.m_dashTimer = float.MinValue;
				Vector3 forward = m_Transform.forward;
				Vector3 forward2 = transform.forward;
				float num = Vector3.Dot(forward, normalized);
				forward -= (num + Mathf.Abs(num)) * normalized;
				m_Transform.forward = forward;
				float num2 = Vector3.Dot(forward2, -normalized);
				forward2 -= (num2 + Mathf.Abs(num2)) * -normalized;
				transform.forward = forward2;
				ApplyImpact(m_controls.Movement.DashImpactData.Multiplier * forward.XZ() * magnitude, m_controls.Movement.DashImpactData.Time);
				clientImpl.ApplyImpact(m_controls.Movement.DashImpactData.Multiplier * forward2.XZ() * magnitude, m_controls.Movement.DashImpactData.Time);
			}
			else
			{
				clientImpl.ApplyImpact(m_controls.Movement.DashImpactData.Multiplier * normalized.XZ() * magnitude, m_controls.Movement.DashImpactData.Time);
			}
		}
	}

	public override void ApplyServerEvent(Serialisable serialisable)
	{
		InputEventMessage inputEventMessage = (InputEventMessage)serialisable;
		InputEventMessage.InputEventType inputEventType = inputEventMessage.inputEventType;
		EntitySerialisationEntry entitySerialisationEntry = null;
		uint entityId = inputEventMessage.entityId;
		if (entityId != 0)
		{
			entitySerialisationEntry = EntitySerialisationRegistry.GetEntry(entityId);
		}
		switch (inputEventType)
		{
		case InputEventMessage.InputEventType.Catch:
			DoCatch();
			break;
		case InputEventMessage.InputEventType.Curse:
			DoCurse();
			break;
		case InputEventMessage.InputEventType.Dash:
			if (m_controlsImpl.m_serverImpl != null || !m_controlsPlayer.IsLocallyControlled())
			{
				DoDash();
			}
			break;
		case InputEventMessage.InputEventType.DashCollision:
			if (m_controlsImpl.m_serverImpl != null || !m_controlsPlayer.IsLocallyControlled())
			{
				PlayerIDProvider playerIDProvider = null;
				if (entitySerialisationEntry != null)
				{
					playerIDProvider = entitySerialisationEntry.m_GameObject.RequestComponent<PlayerIDProvider>();
					DoDashCollisionEffects(playerIDProvider, inputEventMessage.collisionContactPoint);
				}
			}
			break;
		case InputEventMessage.InputEventType.BeginInteraction:
		{
			ClientInteractable interactable2 = null;
			if (entitySerialisationEntry != null)
			{
				interactable2 = entitySerialisationEntry.m_GameObject.GetComponent<ClientInteractable>();
			}
			BeginInteraction(interactable2);
			break;
		}
		case InputEventMessage.InputEventType.EndInteraction:
			EndInteraction();
			break;
		case InputEventMessage.InputEventType.TriggerInteraction:
		{
			ClientInteractable interactable = null;
			if (entitySerialisationEntry != null)
			{
				interactable = entitySerialisationEntry.m_GameObject.GetComponent<ClientInteractable>();
			}
			TriggerInteractable(interactable);
			break;
		}
		case InputEventMessage.InputEventType.EndThrow:
			DoThrow(entitySerialisationEntry.m_GameObject);
			break;
		case InputEventMessage.InputEventType.StartThrow:
			break;
		}
	}

	private void OnCarriedItemChanged(GameObject _before, GameObject _after)
	{
		if (_before != null && _after == null)
		{
			GameUtils.TriggerAudio(GameOneShotAudioTag.PutDown, m_controlObject.layer);
		}
		else if (_before == null && _after != null)
		{
			GameUtils.TriggerAudio(GameOneShotAudioTag.Pickup, m_controlObject.layer);
		}
	}

	private void DoDash()
	{
		GameUtils.TriggerAudio(GameOneShotAudioTag.Dash, m_controlObject.layer);
		m_controls.DashPFXPrefab.InstantiateOnParent(m_Transform);
	}

	private void DoDashCollisionEffects(PlayerIDProvider _otherPlayer, Vector3 _contactPoint)
	{
		GameObject gameObject = UnityEngine.Object.Instantiate(m_controls.ImpactPFXPrefab, _contactPoint, Quaternion.identity);
		GameUtils.TriggerAudio(GameOneShotAudioTag.Impact, m_controlObject.layer);
	}

	private void DoCurse()
	{
		GameUtils.TriggerAudio(GameOneShotAudioTag.Curse, m_controlObject.layer);
		m_controls.CursePFXPrefab.InstantiatePFX(m_Transform);
	}

	private void DoInteraction(ClientInteractable interactable)
	{
		m_interactTriggerCallback(interactable);
	}

	private void DoThrow(GameObject _throwable)
	{
		GameUtils.TriggerAudio(GameOneShotAudioTag.Throw, base.gameObject.layer);
		m_throwTriggerCallback(_throwable);
		OvercookedAchievementManager overcookedAchievementManager = GameUtils.RequestManager<OvercookedAchievementManager>();
		if (overcookedAchievementManager != null)
		{
			ControlPadInput.PadNum padForPlayer = PlayerInputLookup.GetPadForPlayer(m_controlsPlayer.GetID());
			overcookedAchievementManager.IncStat(3, 1f, padForPlayer);
		}
	}

	private void DoCatch()
	{
		GameUtils.TriggerAudio(GameOneShotAudioTag.Catch, base.gameObject.layer);
		Transform transform = m_Transform.FindChildRecursive("Attachment");
		if (null != m_controls.CatchPFXPrefab)
		{
			GameObject gameObject = UnityEngine.Object.Instantiate(m_controls.CatchPFXPrefab, transform.position, Quaternion.identity);
		}
		OvercookedAchievementManager overcookedAchievementManager = GameUtils.RequestManager<OvercookedAchievementManager>();
		if (overcookedAchievementManager != null)
		{
			ControlPadInput.PadNum padForPlayer = PlayerInputLookup.GetPadForPlayer(m_controlsPlayer.GetID());
			overcookedAchievementManager.IncStat(4, 1f, padForPlayer);
		}
	}

	private void BeginInteraction(ClientInteractable _interactable)
	{
		m_lastInteracted = _interactable;
		m_predictedInteracted = _interactable;
		if (_interactable != null)
		{
			_interactable.AddInteractor(base.gameObject);
		}
	}

	private void EndInteraction()
	{
		if (m_lastInteracted != null)
		{
			m_lastInteracted.RemoveInteractor(base.gameObject);
		}
		m_lastInteracted = null;
		m_predictedInteracted = null;
	}

	private void TriggerInteractable(ClientInteractable _interactable)
	{
		if (_interactable != null)
		{
			DoInteraction(_interactable);
		}
	}

	public ClientInteractable GetCurrentlyInteracting()
	{
		if (null != m_lastInteracted)
		{
			return m_lastInteracted;
		}
		if (null != m_predictedInteracted)
		{
			return m_predictedInteracted;
		}
		if (null != m_sessionInteraction)
		{
			return m_sessionInteraction;
		}
		return null;
	}

	public void NotifySessionInteractionStarted(ClientSessionInteractable _interaction)
	{
		if (_interaction != null && _interaction.gameObject != null)
		{
			m_sessionInteraction = _interaction.gameObject.RequireComponent<ClientInteractable>();
			if (m_controlScheme != null)
			{
				m_controlScheme.ClearEvents();
			}
		}
	}

	public void NotifySessionInteractionEnded(ClientSessionInteractable _interaction)
	{
		m_sessionInteraction = null;
		if (m_controlScheme != null)
		{
			m_controlScheme.ClearEvents();
		}
	}

	private void OnThrowableCollision(IClientThrowable _throwable, Collider _collider, Vector3 _contactPoint, Vector3 _contactNormal, Vector3 _relativeVelocity)
	{
		float magnitude = _relativeVelocity.magnitude;
		if (!_throwable.IsFlying() || _throwable.GetThrower() == m_iThrower)
		{
			return;
		}
		GameObject obj = (_throwable as MonoBehaviour).gameObject;
		ICatchable catchable = obj.RequestInterface<ICatchable>();
		bool flag = false;
		if (catchable != null && (catchable as MonoBehaviour).enabled)
		{
			Vector3 position = m_Transform.position;
			Vector3 normalized = m_Transform.forward.WithY(0f).normalized;
			flag = !InteractWithItemHelper.IsColliderInArc(_collider, position, normalized, 2f, (float)Math.PI / 2f);
		}
		else
		{
			flag = true;
		}
		if (flag)
		{
			Vector2 vector = m_controls.Movement.ThrownImpactData.Multiplier * _contactNormal.XZ() * magnitude;
			Vector3 vector2 = _contactPoint - m_Transform.position;
			DoKnockback(ChefEventMessage.KnockbackType.Throw, vector, vector2);
			if (m_controlsImpl.m_serverImpl == null)
			{
				ClientMessenger.ChefKnockbackEventMessage(ChefEventMessage.KnockbackType.Throw, m_entityID, vector, vector2);
			}
			else
			{
				ServerMessenger.SendChefEffectMessage(m_entityID, ChefEffectMessage.EffectType.Impact, vector2);
			}
		}
	}

	public void DoKnockback(ChefEventMessage.KnockbackType _knockbackType, Vector2 _knockBackForce, Vector3 _relativeContactPoint)
	{
		bool flag = true;
		float impactTime = 0f;
		switch (_knockbackType)
		{
		case ChefEventMessage.KnockbackType.Throw:
			flag = m_dashTimer <= 0f;
			impactTime = m_controls.Movement.ThrownImpactData.Time;
			break;
		case ChefEventMessage.KnockbackType.Fire:
			impactTime = m_controls.Movement.HazardImpactData.Time;
			break;
		}
		if (flag)
		{
			ApplyImpact(_knockBackForce, impactTime);
		}
	}

	public void DoImpactEffect(Vector3 _relativePosition)
	{
		GameObject gameObject = UnityEngine.Object.Instantiate(m_controls.ImpactPFXPrefab, m_Transform.position + _relativePosition, Quaternion.identity);
		GameUtils.TriggerAudio(GameOneShotAudioTag.Impact, m_controlObject.layer);
	}

	private void OnStepCollision(Steppable _steppable, Vector3 _contactPoint, Vector3 _contactNormal, Vector3 _relativeVelocity)
	{
		float num = Vector3.Angle(_contactNormal, m_Transform.up);
		if (num > 30f)
		{
			Vector3 direction = _relativeVelocity.SafeNormalised(Vector3.zero);
			if (direction.sqrMagnitude > 0.001f)
			{
				StepOntoObject(_steppable, _contactPoint, direction, m_controls);
			}
		}
	}

	private void OnFireHazardCollision(Vector3 _contactPoint, Vector3 _contactNormal, Vector3 _relativeVelocity)
	{
		float magnitude = _relativeVelocity.magnitude;
		Vector3 input = _contactNormal.WithY(0f).SafeNormalised(-m_Transform.forward);
		Vector2 vector = m_controls.Movement.HazardImpactData.Multiplier * input.XZ() * magnitude;
		Vector3 vector2 = m_Transform.position - _contactPoint;
		DoKnockback(ChefEventMessage.KnockbackType.Fire, vector, vector2);
		if (m_controlsImpl.m_serverImpl == null)
		{
			ClientMessenger.ChefKnockbackEventMessage(ChefEventMessage.KnockbackType.Fire, m_entityID, vector, vector2);
		}
		else
		{
			ServerMessenger.SendChefEffectMessage(m_entityID, ChefEffectMessage.EffectType.Impact, vector2);
		}
	}

	private Vector3 ProgressVelocityWrtFriction(Vector3 _gameplayVelocity, Vector3 _physicalVelocity, Vector3 _targetVelocity, PlayerPhysicsSurfaceProperties _surface)
	{
		Vector3 groundNormal = m_controls.GroundNormal;
		Vector3 normalized = Vector3.Cross(groundNormal, Vector3.forward).normalized;
		Vector3 normalized2 = Vector3.Cross(normalized, groundNormal).normalized;
		Vector3 a = new Vector3(Vector3.Dot(normalized, _gameplayVelocity), Vector3.Dot(groundNormal, _physicalVelocity), Vector3.Dot(normalized2, _gameplayVelocity));
		float num = ((!(_surface != null)) ? 1f : _surface.SpeedMultiplier);
		float value = ((!(_surface != null)) ? 0f : _surface.Slippiness);
		float num2 = ((!(_surface != null)) ? 0f : _surface.Slidiness);
		float num3 = MathUtils.Remap(value, 0f, 1f, 1f, TimeManager.GetDeltaTime(m_controlObject));
		Vector3 vector = new Vector3(num3, 0f, num3);
		Vector3 vector2 = num * _targetVelocity.MultipliedBy(vector) + a.MultipliedBy(Vector3.one - vector);
		Vector3 vector3 = normalized * vector2.x + groundNormal * vector2.y + normalized2 * vector2.z;
		float y = groundNormal.y;
		Vector3 vector4 = groundNormal.WithY(0f).SafeNormalised(Vector3.zero);
		Vector3 vector5 = num2 * (vector4.x * normalized + vector4.z * normalized2);
		float num4 = (1f - y) * Mathf.Clamp01(num2);
		return (1f - num4) * vector3 + num4 * vector5;
	}

	private PlayerPhysicsSurfaceProperties GetSurfaceData()
	{
		if (m_controls.PhysicsSurface != null)
		{
			return m_controls.PhysicsSurface.Properties;
		}
		return null;
	}

	private void StepOntoObject(Steppable _steppable, Vector3 _position, Vector3 _direction, PlayerControls _controls)
	{
		Vector3 forward = m_Transform.forward;
		Vector3 position = m_Transform.position;
		Vector3 vector = _steppable.ProjectPointOntoStep(_position, _direction);
		Vector3 vector2 = vector - position;
		Vector3 vector3 = vector2.WithY(0f).SafeNormalised(forward);
		float num = Vector3.Angle(vector3, forward);
		if (num < 90f && Vector3.Project(vector2, m_Transform.up).sqrMagnitude < _controls.Movement.StepHeightMax * _controls.Movement.StepHeightMax)
		{
			m_Transform.position = vector;
		}
	}

	public void ApplyImpact(Vector2 _xzImpactVelocity, float _impactTime)
	{
		m_impactVelocity = VectorUtils.FromXZ(_xzImpactVelocity, 0f);
		m_impactStartTime = _impactTime;
		m_impactTimer = _impactTime;
	}

	private void ApplyWindForce(float _delta)
	{
		Vector3 velocity = m_controls.WindReceiver.GetVelocity();
		m_controls.Motion.Movement(velocity, _delta);
	}

	private void ApplyGravityForce()
	{
		if (DebugManager.Instance.GetOption("New Gravity"))
		{
			if (m_controls.m_bApplyGravity)
			{
				Vector3 vector = -m_controls.GroundNormal;
				float num = (((int)m_controls.GroundLayer != (int)m_SlopedGroundMask) ? m_controls.Movement.GravityStrength : m_controls.Movement.GravityStrengthSlopedGround);
				m_controls.Motion.Accelerate(vector * num);
			}
		}
		else if (!(m_controls.GroundCollider != null))
		{
			Vector3 vector2 = -Vector3.up;
			m_controls.Motion.Accelerate(vector2 * m_controls.Movement.GravityStrength);
		}
	}

	private void ApplyGroundMovement(float _deltaTime)
	{
		Vector3 velocity = m_controls.SurfaceMovable.GetVelocity();
		m_controls.Motion.Movement(velocity, _deltaTime);
	}

	protected void OnPauseMenuVisibilityChange(BaseMenuBehaviour _menu)
	{
		if (m_controlScheme != null)
		{
			m_controlScheme.ClearEvents();
		}
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		Mailbox.Client.UnregisterForMessageType(MessageType.ChefEffect, m_onChefEffectReceived);
		if (T17InGameFlow.Instance != null)
		{
			T17InGameFlow.Instance.UnRegisterOnPauseMenuVisibilityChanged(OnPauseMenuVisibilityChange);
		}
	}
}
