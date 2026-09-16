using System;
using UnityEngine;

namespace GameModes.Horde
{
	[RequireComponent(typeof(HordeEnemy))]
	public class HordeEnemyCosmeticDecisions : MonoBehaviour, ITriggerReceiver
	{
		[Header("Animation")]
		[SerializeField]
		public Animator m_animator;

		[SerializeField]
		private string m_idleAnimationTrigger = string.Empty;

		[SerializeField]
		private string m_moveAnimationTrigger = string.Empty;

		[SerializeField]
		private string m_spawnAnimationTrigger = string.Empty;

		[SerializeField]
		private string m_attackAnimationTrigger = string.Empty;

		[SerializeField]
		private string m_channelAnimationTrigger = string.Empty;

		[SerializeField]
		private string m_despawnAnimationTrigger = string.Empty;

		[SerializeField]
		private string m_attackAnimationImpactTrigger = string.Empty;

		[SerializeField]
		private string m_spawnAnimationEndTrigger = string.Empty;

		[SerializeField]
		private string m_attackAnimationEndTrigger = string.Empty;

		[SerializeField]
		private string m_despawnAnimationEndTrigger = string.Empty;

		[NonSerialized]
		[HideInInspector]
		public int m_idleAnimationTriggerId;

		[NonSerialized]
		[HideInInspector]
		public int m_moveAnimationTriggerId;

		[NonSerialized]
		[HideInInspector]
		public int m_spawnAnimationTriggerId;

		[NonSerialized]
		[HideInInspector]
		public int m_attackAnimationTriggerId;

		[NonSerialized]
		[HideInInspector]
		public int m_channelAnimationTriggerId;

		[NonSerialized]
		[HideInInspector]
		public int m_despawnAnimationTriggerId;

		[NonSerialized]
		[HideInInspector]
		public int m_attackAnimationImpactTriggerId;

		[NonSerialized]
		[HideInInspector]
		public int m_spawnAnimationEndTriggerId;

		[NonSerialized]
		[HideInInspector]
		public int m_attackAnimationEndTriggerId;

		[NonSerialized]
		[HideInInspector]
		public int m_channelAnimationEndTriggerId;

		[NonSerialized]
		[HideInInspector]
		public int m_despawnAnimationEndTriggerId;

		[Header("Audio")]
		[SerializeField]
		public GameOneShotAudioTag m_onSpawnAudioTag = GameOneShotAudioTag.COUNT;

		[SerializeField]
		public GameOneShotAudioTag m_onAttackAudioTag = GameOneShotAudioTag.COUNT;

		[SerializeField]
		public GameOneShotAudioTag m_onChannelAudioTag = GameOneShotAudioTag.COUNT;

		[SerializeField]
		public GameOneShotAudioTag m_onDespawnAudioTag = GameOneShotAudioTag.COUNT;

		[Header("Effects")]
		[SerializeField]
		public GameObject m_spawnEffectsPrefab;

		[SerializeField]
		public GameObject m_despawnEffectsPrefab;

		public GenericVoid<string> OnTriggerCallback;

		private void Awake()
		{
			m_idleAnimationTriggerId = Animator.StringToHash(m_idleAnimationTrigger);
			m_moveAnimationTriggerId = Animator.StringToHash(m_moveAnimationTrigger);
			m_spawnAnimationTriggerId = Animator.StringToHash(m_spawnAnimationTrigger);
			m_attackAnimationTriggerId = Animator.StringToHash(m_attackAnimationTrigger);
			m_channelAnimationTriggerId = Animator.StringToHash(m_channelAnimationTrigger);
			m_despawnAnimationTriggerId = Animator.StringToHash(m_despawnAnimationTrigger);
			m_attackAnimationImpactTriggerId = Animator.StringToHash(m_attackAnimationImpactTrigger);
			m_spawnAnimationEndTriggerId = Animator.StringToHash(m_spawnAnimationEndTrigger);
			m_attackAnimationEndTriggerId = Animator.StringToHash(m_attackAnimationEndTrigger);
			m_despawnAnimationEndTriggerId = Animator.StringToHash(m_despawnAnimationEndTrigger);
		}

		public void OnTrigger(string trigger)
		{
			OnTriggerCallback(trigger);
		}
	}
}
