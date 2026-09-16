using System.Collections.Generic;
using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

namespace GameModes.Horde
{
	public class ClientHordeTargetCosmeticDecisions : ClientSynchroniserBase
	{
		private HordeTargetCosmeticDecisions m_cosmeticDecisions;

		private ClientHordeTarget m_target;

		private GameObject m_healthUIInstance;

		private HordeTargetUIController m_healthUIController;

		private GameObject m_orderUIInstance;

		private HordeOrderUIController m_orderUIController;

		private List<HordeOrderUIController> m_retiredOrderUIControllers = new List<HordeOrderUIController>();

		private ContextualInteractHoverIcon m_repairHoverIcon;

		private ClientInteractable m_targetInteractable;

		private ClientHordeEnemy m_enemy;

		private ClientHordeEnemyCosmeticDecisions m_enemyCosmetics;

		private bool m_enemyIsAttacking;

		private ClientHordeFlowController m_flowController;

		private float m_previousTargetHealth;

		public override void StartSynchronising(Component synchronisedObject)
		{
			base.StartSynchronising(synchronisedObject);
			m_cosmeticDecisions = (HordeTargetCosmeticDecisions)synchronisedObject;
			m_target = base.gameObject.RequireComponent<ClientHordeTarget>();
			m_previousTargetHealth = m_target.Health;
			m_targetInteractable = base.gameObject.RequireComponent<ClientInteractable>();
			m_repairHoverIcon = base.gameObject.RequestComponent<ContextualInteractHoverIcon>();
			Transform follower = ((!(m_cosmeticDecisions.m_healthUITransform != null)) ? base.transform : m_cosmeticDecisions.m_healthUITransform);
			m_healthUIInstance = GameUtils.InstantiateHoverIconUIController<HordeTargetUIController>(out m_healthUIController, m_cosmeticDecisions.m_healthUIPrefab, follower, "HoverIconCanvas");
			Mailbox.Client.RegisterForMessageType(MessageType.GameState, OnGameStateChanged);
		}

		private void OnGameStateChanged(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
		{
			GameStateMessage gameStateMessage = (GameStateMessage)message;
			if (gameStateMessage.m_State == GameState.StartEntities)
			{
				FlowControllerBase flowControllerBase = GameUtils.RequireManager<FlowControllerBase>();
				m_flowController = flowControllerBase.gameObject.RequireComponent<ClientHordeFlowController>();
				m_flowController.RegisterOnSuccessfulDelivery(null, m_target, OnSuccessfulDelivery);
				m_flowController.RegisterOnIncorrectDelivery(null, m_target, OnIncorrectDelivery);
				m_flowController.RegisterOnEntryAdded(null, m_target, OnEntryAdded);
				m_flowController.RegisterOnEnemyApproaching(null, m_target, OnEnemyApproaching);
				m_flowController.RegisterOnEnemyDespawning(null, m_target, OnEnemyDeath);
			}
		}

		protected override void OnDestroy()
		{
			if (m_flowController != null)
			{
				m_flowController.UnregisterOnSuccessfulDelivery(null, m_target, OnSuccessfulDelivery);
				m_flowController.UnregisterOnIncorrectDelivery(null, m_target, OnIncorrectDelivery);
				m_flowController.UnregisterOnEntryAdded(null, m_target, OnEntryAdded);
				m_flowController.UnregisterOnEnemyApproaching(null, m_target, OnEnemyApproaching);
				m_flowController.UnregisterOnEnemyDeath(null, m_target, OnEnemyDeath);
			}
			Mailbox.Client.UnregisterForMessageType(MessageType.GameState, OnGameStateChanged);
			base.OnDestroy();
		}

		private void OnSuccessfulDelivery(RecipeList.Entry entry)
		{
			if (m_orderUIController != null)
			{
				m_orderUIController.PlayAnimation(new RecipeSuccessAnimation());
				m_retiredOrderUIControllers.Add(m_orderUIController);
				m_orderUIController = null;
				m_orderUIInstance = null;
			}
		}

		private void OnIncorrectDelivery(RecipeList.Entry entry)
		{
			if (m_orderUIController != null)
			{
				m_orderUIController.PlayAnimation(new RecipeFailureAnimation());
			}
		}

		private void OnEntryAdded(RecipeList.Entry entry)
		{
			Transform transform = ((!(m_cosmeticDecisions.m_orderUITransform != null)) ? base.transform : m_cosmeticDecisions.m_orderUITransform);
			m_orderUIInstance = GameUtils.InstantiateHoverIconUIController<HordeOrderUIController>(out m_orderUIController, m_cosmeticDecisions.m_orderUIPrefab, transform, "HoverIconCanvas");
			m_orderUIController.Setup(transform, entry.m_order.m_orderGuiDescription, m_cosmeticDecisions.m_orderUIAnchor);
			m_orderUIController.PlayAnimation(new RecipeAppearAnimation(m_cosmeticDecisions.m_orderAppearAnimationCurve));
		}

		private void OnEnemyApproaching(ClientHordeEnemy enemy)
		{
			m_enemy = enemy;
			m_enemy.RegisterOnBeginState(null, OnEnemyBeginState);
			m_enemyCosmetics = enemy.gameObject.RequireComponent<ClientHordeEnemyCosmeticDecisions>();
			m_enemyCosmetics.RegisterAttackAnimationImpactCallback(null, OnEnemyAttackAnimationImpact);
			m_enemyIsAttacking = false;
			HordeEnemy hordeEnemy = m_enemy.gameObject.RequireComponent<HordeEnemy>();
			float progressWarningAnticipation = (float)hordeEnemy.m_targetDamage / m_target.MaxHealth;
			m_healthUIController.SetProgressWarningAnticipation(progressWarningAnticipation);
			m_healthUIController.SetState(HordeTargetUIController.State.Idle);
		}

		private void OnEnemyDeath(ClientHordeEnemy enemy)
		{
			if (m_target.Health != m_previousTargetHealth)
			{
				OnEnemyAttackAnimationImpact();
			}
			if (m_enemyCosmetics != null)
			{
				m_enemyCosmetics.UnregisterAttackAnimationImpactCallback(null, OnEnemyAttackAnimationImpact);
				m_enemyCosmetics = null;
			}
			if (m_enemy != null)
			{
				m_enemy.UnregisterOnBeginState(null, OnEnemyBeginState);
				m_enemy = null;
			}
			m_healthUIController.SetProgressWarningAnticipation(0f);
			m_cosmeticDecisions.m_animator.SetBool(m_cosmeticDecisions.m_enemyAnimationId, false);
		}

		private void OnEnemyBeginState(ClientHordeEnemy enemy, HordeEnemyBehaviorState fromState, HordeEnemyBehaviorState state)
		{
			m_enemyIsAttacking = state == HordeEnemyBehaviorState.Idle || state == HordeEnemyBehaviorState.Attack || state == HordeEnemyBehaviorState.Channel;
			m_cosmeticDecisions.m_animator.SetBool(m_cosmeticDecisions.m_enemyAnimationId, m_enemyIsAttacking);
		}

		private void OnEnemyAttackAnimationImpact()
		{
			m_cosmeticDecisions.m_animator.SetTrigger(m_cosmeticDecisions.m_hitAnimationId);
			m_cosmeticDecisions.m_animator.SetFloat(m_cosmeticDecisions.m_healthAnimationId, m_target.NormalisedHealth);
			if (m_target.IsAlive)
			{
				if (m_cosmeticDecisions.m_hitParticlePrefab != null)
				{
					GameObject gameObject = Object.Instantiate(m_cosmeticDecisions.m_breakParticlePrefab, base.transform.position, base.transform.rotation);
				}
			}
			else if (m_cosmeticDecisions.m_destroyParticlePrefab != null)
			{
				GameObject gameObject2 = Object.Instantiate(m_cosmeticDecisions.m_destroyParticlePrefab, base.transform.position, base.transform.rotation);
			}
			m_previousTargetHealth = m_target.Health;
		}

		public override void UpdateSynchronising()
		{
			m_healthUIController.SetProgress(m_target.NormalisedHealth);
			bool flag = m_targetInteractable.InteractorCount() > 0;
			if (m_repairHoverIcon != null)
			{
				m_repairHoverIcon.enabled = !flag;
			}
			bool active = m_enemy != null || flag;
			m_healthUIController.gameObject.SetActive(active);
			if (flag)
			{
				m_healthUIController.SetState(HordeTargetUIController.State.Repairing);
				m_cosmeticDecisions.m_animator.SetFloat(m_cosmeticDecisions.m_healthAnimationId, m_target.NormalisedHealth);
			}
			else if (m_enemy != null && (!m_enemyIsAttacking || m_target.IsAlive))
			{
				m_healthUIController.SetState(HordeTargetUIController.State.UnderAttack);
			}
			else if (m_target.IsAlive)
			{
				m_healthUIController.SetState(HordeTargetUIController.State.Idle);
			}
			else
			{
				m_healthUIController.SetState(HordeTargetUIController.State.Broken);
			}
			if (m_retiredOrderUIControllers.Count <= 0)
			{
				return;
			}
			for (int num = m_retiredOrderUIControllers.Count - 1; num >= 0; num--)
			{
				if (!m_retiredOrderUIControllers[num].IsPlayingAnimation())
				{
					GameObject obj = m_retiredOrderUIControllers[num].gameObject;
					m_retiredOrderUIControllers.RemoveAt(num);
					Object.Destroy(obj);
				}
			}
		}
	}
}
