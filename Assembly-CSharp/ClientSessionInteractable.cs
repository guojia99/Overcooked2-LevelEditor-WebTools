using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public abstract class ClientSessionInteractable : ClientSynchroniserBase
{
	protected abstract class SessionBase
	{
		private ClientSessionInteractable m_interactable;

		private PlayerControls m_movement;

		private Rigidbody m_rigidbody;

		private bool m_running = true;

		protected PlayerControls PlayerControls
		{
			get
			{
				return m_movement;
			}
		}

		protected GameObject Avatar
		{
			get
			{
				return m_movement.gameObject;
			}
		}

		public SessionBase(ClientSessionInteractable _self, GameObject _avatar)
		{
			m_interactable = _self;
			m_movement = _avatar.RequireComponent<PlayerControls>();
			m_movement.enabled = false;
			m_rigidbody = _avatar.RequireComponent<Rigidbody>();
			m_rigidbody.velocity = Vector3.zero;
			m_rigidbody.isKinematic = true;
			m_movement.NotifySessionInteractionStarted(m_interactable);
		}

		public virtual void OnSessionEnded()
		{
			if (m_rigidbody != null && Avatar.GetComponent<PlayerIDProvider>().IsLocallyControlled())
			{
				m_rigidbody.isKinematic = false;
			}
			if (m_movement != null)
			{
				m_movement.enabled = true;
				m_movement.NotifySessionInteractionEnded(m_interactable);
			}
		}

		public virtual void Update()
		{
		}
	}

	private ClientInteractable m_interactable;

	private SessionBase m_session;

	private ClientWorldObjectSynchroniser m_ChefWorldObjectSynchroniser;

	public bool HasSession
	{
		get
		{
			return m_session != null;
		}
	}

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
		m_interactable = base.gameObject.GetComponent<ClientInteractable>();
		m_interactable.SetStickyInteractionCallback(InteractionIsSticky);
	}

	public override EntityType GetEntityType()
	{
		return EntityType.SessionInteractable;
	}

	public override void ApplyServerEvent(Serialisable serialisable)
	{
		SessionInteractableMessage sessionInteractableMessage = (SessionInteractableMessage)serialisable;
		if (sessionInteractableMessage.m_msgType == SessionInteractableMessage.MessageType.InteractionState)
		{
			uint interacterID = sessionInteractableMessage.m_interacterID;
			if (interacterID != 0)
			{
				EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(interacterID);
				OnSessionStarted(entry.m_GameObject);
			}
			else
			{
				OnSessionEnded();
			}
		}
	}

	public override void UpdateSynchronising()
	{
		if (m_session != null)
		{
			m_session.Update();
		}
	}

	protected abstract SessionBase BuildSession(GameObject _interacter);

	private void OnSessionStarted(GameObject _avatar)
	{
		m_session = BuildSession(_avatar);
		m_interactable.SetInteractionSuppressed(true);
	}

	private void OnSessionEnded()
	{
		m_session.OnSessionEnded();
		m_session = null;
		m_interactable.SetInteractionSuppressed(false);
	}

	private bool InteractionIsSticky()
	{
		return false;
	}
}
