using System;
using UnityEngine;

namespace GameModes.Horde
{
	public class HordeLockableCosmeticDecisions : MonoBehaviour
	{
		[Header("User Interface")]
		[SerializeField]
		public Sprite m_hoverIcon;

		[SerializeField]
		public GameObject m_hoverIconPrefab;

		[SerializeField]
		public Transform m_hoverIconTarget;

		[SerializeField]
		public Vector2 m_offset;

		[Header("Animation")]
		[SerializeField]
		public Animator m_animator;

		[SerializeField]
		private string m_lockAnimationString = string.Empty;

		[SerializeField]
		private string m_unlockAnimationString = string.Empty;

		[NonSerialized]
		[HideInInspector]
		public int m_lockAnimationId;

		[NonSerialized]
		[HideInInspector]
		public int m_unlockAnimationId;

		[Header("Audio")]
		[SerializeField]
		public GameOneShotAudioTag m_onLockAudioTag = GameOneShotAudioTag.COUNT;

		[SerializeField]
		public GameOneShotAudioTag m_onUnlockAudioTag = GameOneShotAudioTag.COUNT;

		private void Awake()
		{
			m_lockAnimationId = Animator.StringToHash(m_lockAnimationString);
			m_unlockAnimationId = Animator.StringToHash(m_unlockAnimationString);
		}
	}
}
