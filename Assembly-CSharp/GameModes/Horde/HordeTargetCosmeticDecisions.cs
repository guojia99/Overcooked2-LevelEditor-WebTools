using System;
using UnityEngine;

namespace GameModes.Horde
{
	[RequireComponent(typeof(HordeTarget))]
	public class HordeTargetCosmeticDecisions : MonoBehaviour
	{
		[Header("User Interface")]
		[AssignResource("HoverProgressUI", Editorbility.Editable)]
		[SerializeField]
		public GameObject m_healthUIPrefab;

		[SerializeField]
		public Transform m_healthUITransform;

		[AssignResource("horde_order_ui", Editorbility.Editable)]
		[SerializeField]
		public GameObject m_orderUIPrefab;

		[SerializeField]
		public Transform m_orderUITransform;

		[SerializeField]
		public HordeOrderUIController.Align m_orderUIAnchor;

		[Header("Animation")]
		[SerializeField]
		public Animator m_animator;

		[SerializeField]
		private string m_healthAnimationString = string.Empty;

		[NonSerialized]
		[HideInInspector]
		public int m_healthAnimationId;

		[SerializeField]
		private string m_enemyAnimationString = string.Empty;

		[NonSerialized]
		[HideInInspector]
		public int m_enemyAnimationId;

		[SerializeField]
		private string m_hitAnimationString = string.Empty;

		[NonSerialized]
		[HideInInspector]
		public int m_hitAnimationId;

		[SerializeField]
		private string m_repairHitAnimationString = string.Empty;

		[NonSerialized]
		[HideInInspector]
		public int m_repairHitAnimationId;

		[SerializeField]
		public AnimationCurve m_orderAppearAnimationCurve = AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1f);

		[SerializeField]
		public AnimationCurve m_orderHealthWarningAnimationCurve = AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1f);

		[Header("Effects")]
		[SerializeField]
		public GameObject m_hitParticlePrefab;

		[SerializeField]
		public GameObject m_breakParticlePrefab;

		[SerializeField]
		public GameObject m_destroyParticlePrefab;

		public void Awake()
		{
			m_healthAnimationId = Animator.StringToHash(m_healthAnimationString);
			m_enemyAnimationId = Animator.StringToHash(m_enemyAnimationString);
			m_hitAnimationId = Animator.StringToHash(m_hitAnimationString);
			m_repairHitAnimationId = Animator.StringToHash(m_repairHitAnimationString);
		}
	}
}
