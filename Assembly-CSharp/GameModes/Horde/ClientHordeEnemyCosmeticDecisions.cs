using System;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

namespace GameModes.Horde
{
	public class ClientHordeEnemyCosmeticDecisions : ClientSynchroniserBase
	{
		private HordeEnemyCosmeticDecisions m_cosmeticDecisions;

		private ClientHordeEnemy m_enemy;

		private ServerHordeEnemy m_serverEnemy;

		public CallbackVoid m_attackAnimationImpactCallback = delegate
		{
		};

		public void RegisterAttackAnimationImpactCallback(object handle, CallbackVoid onAnimationImpact)
		{
			m_attackAnimationImpactCallback = (CallbackVoid)Delegate.Combine(m_attackAnimationImpactCallback, onAnimationImpact);
		}

		public void UnregisterAttackAnimationImpactCallback(object handle, CallbackVoid onAnimationImpact)
		{
			m_attackAnimationImpactCallback = (CallbackVoid)Delegate.Remove(m_attackAnimationImpactCallback, onAnimationImpact);
		}

		public override void StartSynchronising(Component synchronisedObject)
		{
			base.StartSynchronising(synchronisedObject);
			m_cosmeticDecisions = (HordeEnemyCosmeticDecisions)synchronisedObject;
			HordeEnemyCosmeticDecisions cosmeticDecisions = m_cosmeticDecisions;
			cosmeticDecisions.OnTriggerCallback = (GenericVoid<string>)Delegate.Combine(cosmeticDecisions.OnTriggerCallback, new GenericVoid<string>(OnTrigger));
			m_enemy = base.gameObject.RequireComponent<ClientHordeEnemy>();
			m_enemy.RegisterOnBeginState(this, OnBeginState);
			if (ConnectionStatus.IsHost() || !ConnectionStatus.IsInSession())
			{
				m_serverEnemy = base.gameObject.RequireComponent<ServerHordeEnemy>();
			}
		}

		protected override void OnDestroy()
		{
			HordeEnemyCosmeticDecisions cosmeticDecisions = m_cosmeticDecisions;
			cosmeticDecisions.OnTriggerCallback = (GenericVoid<string>)Delegate.Remove(cosmeticDecisions.OnTriggerCallback, new GenericVoid<string>(OnTrigger));
			m_enemy.UnregisterOnBeginState(this, OnBeginState);
			base.OnDestroy();
		}

		private void OnBeginState(ClientHordeEnemy enemy, HordeEnemyBehaviorState fromState, HordeEnemyBehaviorState state)
		{
			switch (state)
			{
			case HordeEnemyBehaviorState.Spawn:
				m_cosmeticDecisions.m_animator.SetTrigger(m_cosmeticDecisions.m_spawnAnimationTriggerId);
				GameUtils.TriggerAudio(GameOneShotAudioTag.DLC_07_Nombie_Spawn, base.gameObject.layer);
				if (m_cosmeticDecisions.m_spawnEffectsPrefab != null)
				{
					m_cosmeticDecisions.m_spawnEffectsPrefab.InstantiateOnParent(base.gameObject.transform, false);
				}
				break;
			case HordeEnemyBehaviorState.Idle:
				m_cosmeticDecisions.m_animator.SetTrigger(m_cosmeticDecisions.m_idleAnimationTriggerId);
				break;
			case HordeEnemyBehaviorState.Move:
				m_cosmeticDecisions.m_animator.SetTrigger(m_cosmeticDecisions.m_moveAnimationTriggerId);
				break;
			case HordeEnemyBehaviorState.Attack:
				m_cosmeticDecisions.m_animator.SetTrigger(m_cosmeticDecisions.m_attackAnimationTriggerId);
				break;
			case HordeEnemyBehaviorState.Channel:
				m_cosmeticDecisions.m_animator.SetTrigger(m_cosmeticDecisions.m_channelAnimationTriggerId);
				break;
			case HordeEnemyBehaviorState.Despawn:
				m_cosmeticDecisions.m_animator.SetTrigger(m_cosmeticDecisions.m_despawnAnimationTriggerId);
				GameUtils.TriggerAudio(GameOneShotAudioTag.DLC_07_Nombie_Despawn, base.gameObject.layer);
				if (m_cosmeticDecisions.m_despawnEffectsPrefab != null)
				{
					GameObject gameObject = m_cosmeticDecisions.m_despawnEffectsPrefab.InstantiateOnParent(base.gameObject.transform.parent, false);
					gameObject.transform.SetPositionAndRotation(base.gameObject.transform.position, base.gameObject.transform.rotation);
				}
				break;
			case HordeEnemyBehaviorState.Despawned:
				break;
			}
		}

		private void OnTrigger(string trigger)
		{
			int num = Animator.StringToHash(trigger);
			if (m_serverEnemy != null)
			{
				if (num == m_cosmeticDecisions.m_spawnAnimationEndTriggerId)
				{
					m_serverEnemy.Transition(HordeEnemyBehaviorState.Move);
				}
				else if (num == m_cosmeticDecisions.m_attackAnimationEndTriggerId)
				{
					m_serverEnemy.Transition(HordeEnemyBehaviorState.Idle);
				}
				else if (num == m_cosmeticDecisions.m_despawnAnimationEndTriggerId)
				{
					m_serverEnemy.Transition(HordeEnemyBehaviorState.Despawned);
				}
			}
			if (num == m_cosmeticDecisions.m_attackAnimationImpactTriggerId)
			{
				m_attackAnimationImpactCallback();
			}
		}
	}
}
