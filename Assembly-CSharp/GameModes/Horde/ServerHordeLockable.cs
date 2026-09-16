using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

namespace GameModes.Horde
{
	public class ServerHordeLockable : ServerSynchroniserBase
	{
		private HordeLockable m_lockable;

		private ServerInteractable m_interactable;

		private ServerHordeFlowController m_flowController;

		private HordeLockableMessage m_message = new HordeLockableMessage();

		private bool m_locked = true;

		public override EntityType GetEntityType()
		{
			return EntityType.HordeLockable;
		}

		public override void StartSynchronising(Component synchronisedObject)
		{
			base.StartSynchronising(synchronisedObject);
			m_lockable = (HordeLockable)synchronisedObject;
			m_interactable = base.gameObject.RequestComponent<ServerInteractable>();
			if (m_interactable != null)
			{
				m_interactable.RegisterCallbacks(OnBeginInteract, null);
				m_interactable.SetInteractionSuppressed(false);
			}
			Mailbox.Server.RegisterForMessageType(MessageType.GameState, OnGameStateChanged);
		}

		public override void OnDestroy()
		{
			base.OnDestroy();
			Mailbox.Server.UnregisterForMessageType(MessageType.GameState, OnGameStateChanged);
		}

		private void OnGameStateChanged(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
		{
			GameStateMessage gameStateMessage = (GameStateMessage)message;
			if (gameStateMessage.m_State == GameState.StartedEntities)
			{
				FlowControllerBase flowControllerBase = GameUtils.RequireManager<FlowControllerBase>();
				m_flowController = flowControllerBase.gameObject.RequireComponent<ServerHordeFlowController>();
			}
		}

		private void OnBeginInteract(GameObject interacter, Vector2 dir)
		{
			if (m_locked && m_flowController.SpendMoney(m_lockable.m_unlockCost))
			{
				Unlock();
			}
		}

		public void Lock()
		{
			if (m_locked)
			{
				return;
			}
			m_locked = true;
			if (m_interactable != null)
			{
				m_interactable.SetInteractionSuppressed(false);
			}
			HordeLockableMessage.Lock(ref m_message);
			SendServerEvent(m_message);
			if (m_lockable.m_lockables == null || m_lockable.m_lockables.Length <= 0)
			{
				return;
			}
			for (int i = 0; i < m_lockable.m_lockables.Length; i++)
			{
				if (m_lockable.m_lockables[i] != null)
				{
					m_lockable.m_lockables[i].gameObject.RequireComponent<ServerHordeLockable>().Lock();
				}
			}
		}

		public void Unlock()
		{
			if (!m_locked)
			{
				return;
			}
			m_locked = false;
			if (m_interactable != null)
			{
				m_interactable.SetInteractionSuppressed(true);
			}
			HordeLockableMessage.Unlock(ref m_message);
			SendServerEvent(m_message);
			if (m_lockable.m_lockables == null || m_lockable.m_lockables.Length <= 0)
			{
				return;
			}
			for (int i = 0; i < m_lockable.m_lockables.Length; i++)
			{
				if (m_lockable.m_lockables[i] != null)
				{
					m_lockable.m_lockables[i].gameObject.RequireComponent<ServerHordeLockable>().Unlock();
				}
			}
		}
	}
}
