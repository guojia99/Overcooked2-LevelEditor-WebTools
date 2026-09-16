using System.Collections.Generic;
using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public abstract class ServerSessionInteractable : ServerSynchroniserBase
{
	protected abstract class SessionBase
	{
		private GameObject gameObject;

		private PlayerControls m_movement;

		protected bool m_running = true;

		private CollisionRecorder m_CollisionRecorder;

		private int m_PlayerLayer;

		private Rigidbody m_RigidBody;

		protected PlayerControls PlayerControls
		{
			get
			{
				return m_movement;
			}
		}

		public GameObject Avatar
		{
			get
			{
				return m_movement.gameObject;
			}
		}

		public bool IsRunning
		{
			get
			{
				return m_running;
			}
		}

		public SessionBase(ServerSessionInteractable _self, GameObject _avatar)
		{
			m_PlayerLayer = LayerMask.NameToLayer("Players");
			gameObject = _self.gameObject;
			m_movement = _avatar.RequireComponent<PlayerControls>();
			m_movement.enabled = false;
			m_CollisionRecorder = _avatar.RequireComponent<CollisionRecorder>();
			m_CollisionRecorder.SetFilter(CollisionFilter);
			m_RigidBody = _avatar.RequireComponent<Rigidbody>();
			m_RigidBody.isKinematic = true;
			m_movement.ControlScheme.ClearEvents();
			Mailbox.Client.RegisterForMessageType(MessageType.DestroyChef, OnDestroyChefMessageReceived);
		}

		public virtual void OnSessionEnded()
		{
			m_running = false;
			if (m_CollisionRecorder != null)
			{
				m_CollisionRecorder.SetFilter(null);
			}
			if (m_RigidBody != null)
			{
				m_RigidBody.isKinematic = false;
			}
			if (m_movement != null)
			{
				m_movement.enabled = true;
			}
			Mailbox.Client.UnregisterForMessageType(MessageType.DestroyChef, OnDestroyChefMessageReceived);
		}

		public virtual void OnDestroyChefMessageReceived(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
		{
			DestroyChefMessage destroyChefMessage = (DestroyChefMessage)message;
			EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(destroyChefMessage.m_Chef.m_Header.m_uEntityID);
			if (entry != null && entry.m_GameObject != null)
			{
				PlayerControls playerControls = entry.m_GameObject.RequireComponent<PlayerControls>();
				if (m_movement == playerControls)
				{
					OnSessionEnded();
				}
			}
		}

		private bool CollisionFilter(Collision _collision)
		{
			if (_collision != null && _collision.gameObject != null && _collision.rigidbody != null)
			{
				return _collision.gameObject.layer == m_PlayerLayer && _collision.rigidbody.velocity.sqrMagnitude > 0.001f;
			}
			return false;
		}

		public virtual void Update()
		{
			if ((m_movement == null || m_movement.ControlScheme.m_pickupButton.JustPressed() || m_movement.ControlScheme.m_worksurfaceUseButton.JustPressed() || m_movement.ControlScheme.m_dashButton.JustPressed() || !m_movement.GetDirectlyUnderPlayerControl()) && m_running)
			{
				OnSessionEnded();
			}
			if (!m_running)
			{
				return;
			}
			List<Collision> recentCollisions = m_CollisionRecorder.GetRecentCollisions();
			for (int i = 0; i < recentCollisions.Count; i++)
			{
				PlayerControls component = recentCollisions[i].gameObject.GetComponent<PlayerControls>();
				if (null != component && component.IsDashing())
				{
					OnSessionEnded();
					break;
				}
			}
		}
	}

	private SessionInteractable m_sessionInteractable;

	private ServerInteractable m_interactable;

	private SessionBase m_session;

	private SessionInteractableMessage m_data = new SessionInteractableMessage();

	protected SessionBase Session
	{
		get
		{
			return m_session;
		}
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_sessionInteractable = (SessionInteractable)synchronisedObject;
		m_interactable = base.gameObject.GetComponent<ServerInteractable>();
		m_interactable.RegisterTriggerCallbacks(StartSession);
		m_interactable.RegisterCanInteractCallbacks(CanInteract);
	}

	public override EntityType GetEntityType()
	{
		return EntityType.SessionInteractable;
	}

	private void SynchroniseInteractionState(GameObject _interacter)
	{
		m_data.m_msgType = SessionInteractableMessage.MessageType.InteractionState;
		if ((bool)_interacter)
		{
			EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(_interacter);
			uint uEntityID = entry.m_Header.m_uEntityID;
			m_data.m_interacterID = uEntityID;
		}
		else
		{
			m_data.m_interacterID = 0u;
		}
		SendServerEvent(m_data);
	}

	protected abstract SessionBase BuildSession(GameObject _interacter);

	protected virtual void Awake()
	{
		base.enabled = false;
	}

	protected virtual bool CanInteract(GameObject _interacter)
	{
		ICarrier carrier = _interacter.RequireInterface<ICarrier>();
		if (carrier == null || carrier.InspectCarriedItem() != null)
		{
			return false;
		}
		return true;
	}

	protected virtual void StartSession(GameObject _interacter, Vector2 _directionXZ)
	{
		m_interactable.SetInteractionSuppressed(true);
		SynchroniseInteractionState(_interacter);
		m_session = BuildSession(_interacter);
		base.enabled = true;
		if (m_sessionInteractable.m_onSessionBegun != string.Empty)
		{
			SendMessage("OnTrigger", m_sessionInteractable.m_onSessionBegun, SendMessageOptions.DontRequireReceiver);
		}
	}

	protected virtual void OnSessionEnded()
	{
		SynchroniseInteractionState(null);
		m_interactable.SetInteractionSuppressed(false);
		m_session = null;
		base.enabled = false;
		if (m_sessionInteractable.m_onSesionEnded != string.Empty)
		{
			SendMessage("OnTrigger", m_sessionInteractable.m_onSesionEnded, SendMessageOptions.DontRequireReceiver);
		}
	}

	public override void UpdateSynchronising()
	{
		m_session.Update();
		if (!m_session.IsRunning)
		{
			OnSessionEnded();
		}
	}

	public override void OnDestroy()
	{
		base.OnDestroy();
		if (m_interactable != null)
		{
			m_interactable.UnregisterTriggerCallbacks(StartSession);
			m_interactable.UnregisterCanInteractCallbacks(CanInteract);
		}
	}
}
