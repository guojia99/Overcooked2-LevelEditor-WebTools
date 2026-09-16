using System;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

namespace GameModes.Horde
{
	public class ClientHordeEnemy : ClientSynchroniserBase
	{
		private HordeEnemy m_enemy;

		private HordeEnemyMessage m_message = default(HordeEnemyMessage);

		private HordeStateMachine<HordeEnemyBehaviorState> m_stateMachine;

		private ClientHordeTarget m_target;

		public GenericVoid<ClientHordeEnemy, HordeEnemyBehaviorState, HordeEnemyBehaviorState> m_onBeginState = delegate
		{
		};

		private float m_time;

		public bool IsAlive
		{
			get
			{
				return m_stateMachine.StateId != HordeEnemyBehaviorState.Despawned;
			}
		}

		public void RegisterOnBeginState(object handle, GenericVoid<ClientHordeEnemy, HordeEnemyBehaviorState, HordeEnemyBehaviorState> onBeginState)
		{
			m_onBeginState = (GenericVoid<ClientHordeEnemy, HordeEnemyBehaviorState, HordeEnemyBehaviorState>)Delegate.Combine(m_onBeginState, onBeginState);
		}

		public void UnregisterOnBeginState(object handle, GenericVoid<ClientHordeEnemy, HordeEnemyBehaviorState, HordeEnemyBehaviorState> onBeginState)
		{
			m_onBeginState = (GenericVoid<ClientHordeEnemy, HordeEnemyBehaviorState, HordeEnemyBehaviorState>)Delegate.Remove(m_onBeginState, onBeginState);
		}

		public override EntityType GetEntityType()
		{
			return EntityType.HordeEnemy;
		}

		public override void StartSynchronising(Component synchronisedObject)
		{
			base.StartSynchronising(synchronisedObject);
			m_enemy = (HordeEnemy)synchronisedObject;
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
			if (ConnectionStatus.IsHost() || !ConnectionStatus.IsInSession())
			{
				m_stateMachine = new HordeStateMachine<HordeEnemyBehaviorState>(HordeEnemyBehaviorState.Start, states, OnBeginStateServerLocalClient, null, OnUpdateState);
			}
			else
			{
				m_stateMachine = new HordeStateMachine<HordeEnemyBehaviorState>(HordeEnemyBehaviorState.Start, states, OnBeginStateRemoteClient, null, OnUpdateState);
			}
		}

		public override void ApplyServerEvent(Serialisable serialisable)
		{
			HordeEnemyMessage hordeEnemyMessage = (HordeEnemyMessage)(object)serialisable;
			HordeEnemyMessage.Kind kind = hordeEnemyMessage.m_kind;
			if (kind == HordeEnemyMessage.Kind.Transition)
			{
				m_stateMachine.Transition(hordeEnemyMessage.m_toState);
			}
		}

		public override void UpdateSynchronising()
		{
			if (m_stateMachine != null)
			{
				m_stateMachine.Tick(TimeManager.GetDeltaTime(base.gameObject));
			}
		}

		public void Setup(ClientHordeFlowController flowController, ClientHordeTarget target)
		{
			m_target = target;
		}

		private void OnBeginStateServerLocalClient(IHordeStateMachine<HordeEnemyBehaviorState> stateMachine, HordeEnemyBehaviorState fromState, HordeEnemyBehaviorState toState)
		{
			ServerHordeEnemy serverHordeEnemy = base.gameObject.RequireComponent<ServerHordeEnemy>();
			serverHordeEnemy.Transition(toState);
			m_onBeginState(this, fromState, toState);
		}

		private void OnBeginStateRemoteClient(IHordeStateMachine<HordeEnemyBehaviorState> stateMachine, HordeEnemyBehaviorState fromState, HordeEnemyBehaviorState toState)
		{
			m_onBeginState(this, fromState, toState);
		}

		private void OnUpdateState(IHordeStateMachine<HordeEnemyBehaviorState> stateMachine, HordeEnemyBehaviorState state, float dT)
		{
			m_time += dT;
			if (state == HordeEnemyBehaviorState.Move)
			{
				if (VectorUtils.DistanceSq(base.transform.position, m_target.TargetTransform.position) > m_target.TargetRadius + m_target.TargetRadius)
				{
					float num = m_enemy.m_movementCurve.Evaluate(m_time % 1f) * m_enemy.m_movementSpeed;
					base.transform.position = Vector3.MoveTowards(base.transform.position, m_target.TargetTransform.position, num * dT);
				}
				else
				{
					m_stateMachine.Transition(HordeEnemyBehaviorState.Idle);
					m_time = 0f;
				}
			}
		}
	}
}
