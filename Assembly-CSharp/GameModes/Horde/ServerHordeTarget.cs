using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

namespace GameModes.Horde
{
	public class ServerHordeTarget : ServerSynchroniserBase
	{
		private HordeTarget m_target;

		private HordeTargetMessage m_message = default(HordeTargetMessage);

		private ServerInteractable m_interactable;

		private int m_interactors;

		private ServerHordeFlowController m_flowController;

		private HordeLevelConfig m_levelConfig;

		private float m_repairAmount;

		public float Health { get; private set; }

		public float MaxHealth
		{
			get
			{
				return m_levelConfig.m_targetHealth;
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

		public override EntityType GetEntityType()
		{
			return EntityType.HordeTarget;
		}

		public override void StartSynchronising(Component synchronisedObject)
		{
			base.StartSynchronising(synchronisedObject);
			m_target = (HordeTarget)synchronisedObject;
			m_interactable = base.gameObject.RequireComponent<ServerInteractable>();
			m_interactable.RegisterCallbacks(OnBeginInteract, OnEndInteract);
			m_levelConfig = GameUtils.GetLevelConfig() as HordeLevelConfig;
			Health = m_levelConfig.m_targetHealth;
			Mailbox.Server.RegisterForMessageType(MessageType.GameState, OnGameStateChanged);
		}

		private void OnGameStateChanged(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
		{
			GameStateMessage gameStateMessage = (GameStateMessage)message;
			if (gameStateMessage.m_State == GameState.StartedEntities)
			{
				FlowControllerBase flowControllerBase = GameUtils.RequireManager<FlowControllerBase>();
				m_flowController = flowControllerBase.gameObject.RequireComponent<ServerHordeFlowController>();
				m_flowController.RegisterOnMoneyChanged(null, OnPlayerMoneyChanged);
				m_interactable.SetStickyInteractionCallback(() => true);
				m_interactable.SetInteractionSuppressed(true);
			}
		}

		public override void OnDestroy()
		{
			Mailbox.Server.UnregisterForMessageType(MessageType.GameState, OnGameStateChanged);
			base.OnDestroy();
			if (m_flowController != null)
			{
				m_flowController.UnregisterOnMoneyChanged(null, OnPlayerMoneyChanged);
			}
		}

		public void Damage(float targetDamage)
		{
			Health = Mathf.Max(Health - targetDamage, 0f);
			m_interactable.SetInteractionSuppressed(Health >= MaxHealth);
			HordeTargetMessage.Health(ref m_message, Health);
			SendServerEvent(m_message);
		}

		private void OnBeginInteract(GameObject interacter, Vector2 dir)
		{
			if (Health < MaxHealth && m_flowController.Money > 0)
			{
				m_interactors++;
			}
		}

		private void OnEndInteract(GameObject interacter)
		{
			if (m_interactors <= 0)
			{
				return;
			}
			m_interactors--;
			if (m_repairAmount > 0f)
			{
				int amount = Mathf.CeilToInt(Mathf.Clamp01(m_repairAmount / MaxHealth) * (float)m_levelConfig.m_targetRepairCostMax);
				if (m_flowController.SpendMoney(amount))
				{
					Health = Mathf.Min(Health + m_repairAmount, MaxHealth);
					m_interactable.SetInteractionSuppressed(!CanInteract());
					HordeTargetMessage.Health(ref m_message, Health);
					SendServerEvent(m_message);
				}
				m_repairAmount = 0f;
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

		public override void UpdateSynchronising()
		{
			if (m_interactors <= 0 || !(Health < MaxHealth))
			{
				return;
			}
			float num = m_levelConfig.m_targetRepairSpeed * (float)m_interactors * TimeManager.GetDeltaTime(base.gameObject);
			m_repairAmount += num;
			if (m_repairAmount >= m_levelConfig.m_targetRepairThreshold)
			{
				float num2 = Mathf.Clamp01(m_repairAmount / MaxHealth) * (float)m_levelConfig.m_targetRepairCostMax;
				if (m_flowController.SpendMoney((num2 < 1f) ? 1 : ((int)num2)))
				{
					Health = Mathf.Min(Health + m_repairAmount, MaxHealth);
					m_interactable.SetInteractionSuppressed(!CanInteract());
					HordeTargetMessage.Health(ref m_message, Health);
					SendServerEvent(m_message);
				}
				m_repairAmount = 0f;
			}
		}

		public Vector3 GenerateSpawnPosition(out Quaternion dir)
		{
			Vector3 input = Random.insideUnitCircle;
			input = input.XZY();
			input = input * SpawnRadius + SpawnTransform.position;
			dir = Quaternion.LookRotation(input - TargetTransform.position);
			return input;
		}
	}
}
