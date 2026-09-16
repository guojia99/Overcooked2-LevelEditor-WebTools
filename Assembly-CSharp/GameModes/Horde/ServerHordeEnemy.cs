using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

namespace GameModes.Horde
{
	public class ServerHordeEnemy : ServerSynchroniserBase
	{
		private HordeEnemy m_enemy;

		private int m_recipeCount = 1;

		private HordeEnemyMessage m_message = default(HordeEnemyMessage);

		private HordeStateMachine<HordeEnemyBehaviorState> m_stateMachine;

		private ServerHordeFlowController m_flowController;

		private ServerHordeTarget m_target;

		private HordeLevelConfig m_levelConfig;

		private bool m_beginStateMachine;

		private float m_attackTimer;

		public bool IsAlive
		{
			get
			{
				return m_stateMachine.StateId != HordeEnemyBehaviorState.Despawned;
			}
		}

		public override EntityType GetEntityType()
		{
			return EntityType.HordeEnemy;
		}

		public override void StartSynchronising(Component synchronisedObject)
		{
			base.StartSynchronising(synchronisedObject);
			m_enemy = (HordeEnemy)synchronisedObject;
			m_recipeCount = m_enemy.m_recipeCount;
			m_levelConfig = GameUtils.GetLevelConfig() as HordeLevelConfig;
			HordeState<HordeEnemyBehaviorState>[] states = new HordeState<HordeEnemyBehaviorState>[8]
			{
				new HordeState<HordeEnemyBehaviorState>(HordeEnemyBehaviorState.Start),
				new HordeState<HordeEnemyBehaviorState>(HordeEnemyBehaviorState.Spawn),
				new HordeState<HordeEnemyBehaviorState>(HordeEnemyBehaviorState.Idle),
				new HordeState<HordeEnemyBehaviorState>(HordeEnemyBehaviorState.Move),
				new HordeState<HordeEnemyBehaviorState>(HordeEnemyBehaviorState.Attack),
				new HordeState<HordeEnemyBehaviorState>(HordeEnemyBehaviorState.Channel),
				new HordeState<HordeEnemyBehaviorState>(HordeEnemyBehaviorState.Despawn),
				new HordeState<HordeEnemyBehaviorState>(HordeEnemyBehaviorState.Despawned)
			};
			m_stateMachine = new HordeStateMachine<HordeEnemyBehaviorState>(HordeEnemyBehaviorState.Start, states, null, null, OnUpdateState);
		}

		public override void UpdateSynchronising()
		{
			if (!m_beginStateMachine)
			{
				Transition(HordeEnemyBehaviorState.Spawn);
				m_beginStateMachine = true;
			}
			if (m_stateMachine != null)
			{
				m_stateMachine.Tick(TimeManager.GetDeltaTime(base.gameObject));
			}
		}

		public void Setup(ServerHordeFlowController flowController, ServerHordeTarget target)
		{
			m_flowController = flowController;
			m_target = target;
		}

		private void OnUpdateState(IHordeStateMachine<HordeEnemyBehaviorState> stateMachine, HordeEnemyBehaviorState state, float dT)
		{
			switch (state)
			{
			case HordeEnemyBehaviorState.Idle:
				m_attackTimer += dT;
				if (!m_target.IsAlive)
				{
					Transition(HordeEnemyBehaviorState.Channel);
					m_attackTimer = 0f;
				}
				else if (m_target.IsAlive && m_attackTimer >= m_enemy.m_attackTargetFrequencySeconds)
				{
					Transition(HordeEnemyBehaviorState.Attack);
					m_attackTimer = 0f;
				}
				break;
			case HordeEnemyBehaviorState.Attack:
				m_target.Damage(m_enemy.m_targetDamage);
				Transition(HordeEnemyBehaviorState.Idle);
				break;
			case HordeEnemyBehaviorState.Channel:
				m_attackTimer += dT;
				if (m_target.IsAlive)
				{
					Transition(HordeEnemyBehaviorState.Idle);
					m_attackTimer = 0f;
				}
				else if (m_attackTimer >= m_enemy.m_attackKitchenFrequencySeconds)
				{
					m_flowController.Damage(m_enemy.m_kitchenDamage);
					m_attackTimer = 0f;
				}
				break;
			}
		}

		public void Transition(HordeEnemyBehaviorState toState)
		{
			if (m_stateMachine.Transition(toState))
			{
				HordeEnemyMessage.Transition(ref m_message, toState);
				SendServerEvent(m_message);
			}
		}

		public bool Feed(RecipeList.Entry entry)
		{
			m_recipeCount--;
			if (m_recipeCount <= 0)
			{
				Transition(HordeEnemyBehaviorState.Despawn);
				return true;
			}
			return false;
		}
	}
}
