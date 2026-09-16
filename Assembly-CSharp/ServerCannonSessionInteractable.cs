using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerCannonSessionInteractable : ServerSessionInteractable
{
	private class UserSession : SessionBase
	{
		private ServerCannon m_cannon;

		private GameObject m_avatar;

		public UserSession(ServerCannonSessionInteractable _self, GameObject _avatar, ServerCannon _cannon)
			: base(_self, _avatar)
		{
			m_avatar = _avatar;
			m_cannon = _cannon;
			m_cannon.Load(_avatar, InteractionEnded);
		}

		public override void Update()
		{
			if (base.PlayerControls != null)
			{
				base.PlayerControls.ControlScheme.IsUseJustReleased();
			}
			if ((base.PlayerControls == null || base.PlayerControls.ControlScheme.m_dashButton.JustPressed()) && !m_cannon.IsFlying())
			{
				m_cannon.Unload(m_avatar);
			}
		}

		public override void OnDestroyChefMessageReceived(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
		{
			DestroyChefMessage destroyChefMessage = (DestroyChefMessage)message;
			EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(destroyChefMessage.m_Chef.m_Header.m_uEntityID);
			if (entry != null && entry.m_GameObject != null)
			{
				PlayerControls playerControls = entry.m_GameObject.RequireComponent<PlayerControls>();
				if (base.PlayerControls == playerControls)
				{
					m_cannon.Unload(entry.m_GameObject);
				}
			}
		}

		public void InteractionEnded(bool enableComponents)
		{
			if (enableComponents)
			{
				OnSessionEnded();
				return;
			}
			m_running = false;
			Mailbox.Client.UnregisterForMessageType(MessageType.DestroyChef, OnDestroyChefMessageReceived);
		}
	}

	private UserSession m_session;

	private ServerCannon m_cannon;

	private ServerPlacementInteractable m_placementInteractable;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_cannon = synchronisedObject.gameObject.RequireComponent<ServerCannon>();
		m_placementInteractable = base.gameObject.RequireComponent<ServerPlacementInteractable>();
		m_placementInteractable.RegisterCanInteractCallback(CanInteract);
		m_placementInteractable.RegisterTriggerCallback(StartSession);
	}

	protected override bool CanInteract(GameObject _interacter)
	{
		return !m_cannon.IsFlying();
	}

	protected override void StartSession(GameObject _interacter, Vector2 _directionXZ)
	{
		m_placementInteractable.SetInteractionSurpressed(true);
		base.StartSession(_interacter, _directionXZ);
	}

	protected override SessionBase BuildSession(GameObject _interacter)
	{
		m_session = new UserSession(this, _interacter, m_cannon);
		return m_session;
	}

	public void Exit()
	{
		m_session.OnSessionEnded();
	}

	protected override void OnSessionEnded()
	{
		m_placementInteractable.SetInteractionSurpressed(false);
		base.OnSessionEnded();
	}
}
