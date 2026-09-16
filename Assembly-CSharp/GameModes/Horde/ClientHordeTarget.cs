using System;
using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

namespace GameModes.Horde
{
	public class ClientHordeTarget : ClientSynchroniserBase
	{
		private HordeTarget m_target;

		private ClientInteractable m_interactable;

		private ClientHordeFlowController m_flowController;

		private HordeLevelConfig m_levelConfig;

		public GenericVoid<ClientHordeTarget, float, float> m_onDamaged = delegate
		{
		};

		public float Health { get; private set; }

		public float MaxHealth
		{
			get
			{
				return m_levelConfig.m_targetHealth;
			}
		}

		public float NormalisedHealth
		{
			get
			{
				return Health / MaxHealth;
			}
		}

		public bool IsAlive
		{
			get
			{
				return Health > 0f;
			}
		}

		public Transform TargetTransform
		{
			get
			{
				return m_target.TargetTransform;
			}
		}

		public float TargetRadius
		{
			get
			{
				return m_target.TargetRadius;
			}
		}

		public Transform SpawnTransform
		{
			get
			{
				return m_target.SpawnTransform;
			}
		}

		public float SpawnRadius
		{
			get
			{
				return m_target.SpawnRadius;
			}
		}

		public void RegisterOnDamaged(object handle, GenericVoid<ClientHordeTarget, float, float> onDamaged)
		{
			m_onDamaged = (GenericVoid<ClientHordeTarget, float, float>)Delegate.Combine(m_onDamaged, onDamaged);
		}

		public void UnregisterOnDamaged(object handle, GenericVoid<ClientHordeTarget, float, float> onDamaged)
		{
			m_onDamaged = (GenericVoid<ClientHordeTarget, float, float>)Delegate.Remove(m_onDamaged, onDamaged);
		}

		public override EntityType GetEntityType()
		{
			return EntityType.HordeTarget;
		}

		public override void StartSynchronising(Component synchronisedObject)
		{
			base.StartSynchronising(synchronisedObject);
			m_target = (HordeTarget)synchronisedObject;
			m_interactable = base.gameObject.RequireComponent<ClientInteractable>();
			m_levelConfig = GameUtils.GetLevelConfig() as HordeLevelConfig;
			Health = m_levelConfig.m_targetHealth;
			Mailbox.Client.RegisterForMessageType(MessageType.GameState, OnGameStateChanged);
		}

		protected override void OnDestroy()
		{
			base.OnDestroy();
			Mailbox.Client.UnregisterForMessageType(MessageType.GameState, OnGameStateChanged);
			if (m_flowController != null)
			{
				m_flowController.UnregisterOnMoneyChanged(null, OnPlayerMoneyChanged);
			}
		}

		private void OnGameStateChanged(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
		{
			GameStateMessage gameStateMessage = (GameStateMessage)message;
			if (gameStateMessage.m_State == GameState.StartEntities)
			{
				FlowControllerBase flowControllerBase = GameUtils.RequireManager<FlowControllerBase>();
				m_flowController = flowControllerBase.gameObject.RequireComponent<ClientHordeFlowController>();
				m_flowController.RegisterOnMoneyChanged(null, OnPlayerMoneyChanged);
				m_interactable.SetStickyInteractionCallback(() => true);
				m_interactable.SetInteractionSuppressed(true);
			}
		}

		public override void ApplyServerEvent(Serialisable serialisable)
		{
			HordeTargetMessage hordeTargetMessage = (HordeTargetMessage)(object)serialisable;
			HordeTargetMessage.Kind kind = hordeTargetMessage.m_kind;
			if (kind == HordeTargetMessage.Kind.Health)
			{
				Health = hordeTargetMessage.m_health;
				m_interactable.SetInteractionSuppressed(!CanInteract());
				m_onDamaged(this, Health, MaxHealth);
			}
		}

		private bool CanInteract()
		{
			return Health < MaxHealth && m_flowController.Money > 0;
		}

		private void OnPlayerMoneyChanged(int _money)
		{
			m_interactable.SetInteractionSuppressed(!CanInteract());
		}
	}
}
