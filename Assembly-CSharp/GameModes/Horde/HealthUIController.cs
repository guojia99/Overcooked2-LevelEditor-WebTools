using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;
using UnityEngine.Serialization;

namespace GameModes.Horde
{
	public class HealthUIController : UIControllerBase
	{
		[SerializeField]
		[AssignResource("RemovePointsFloatingNumberUI", Editorbility.Editable)]
		private GameObject m_removePointsFloatingTextPrefab;

		[SerializeField]
		private Vector2 m_floatingTextOffset = new Vector2(0.5f, 0.5f);

		[SerializeField]
		private ProgressBarUI m_healthBar;

		[SerializeField]
		private Animator m_animator;

		[SerializeField]
		private float m_pulseStart = 0.5f;

		[FormerlySerializedAs("m_heartPulseTriggerString")]
		[SerializeField]
		private string m_healthAlertString = string.Empty;

		private int m_healthAlertId;

		[SerializeField]
		private string m_healthPulseString = string.Empty;

		private int m_healthPulseId;

		private ClientHordeFlowController m_flowController;

		private float m_health;

		private void Awake()
		{
			m_healthBar.Value = 1f;
			m_healthAlertId = Animator.StringToHash(m_healthAlertString);
			m_healthPulseId = Animator.StringToHash(m_healthPulseString);
			Mailbox.Client.RegisterForMessageType(MessageType.GameState, OnGameStateChanged);
		}

		private void OnDestroy()
		{
			Mailbox.Client.UnregisterForMessageType(MessageType.GameState, OnGameStateChanged);
		}

		private void OnGameStateChanged(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
		{
			GameStateMessage gameStateMessage = (GameStateMessage)message;
			if (gameStateMessage.m_State == GameState.StartEntities)
			{
				FlowControllerBase flowControllerBase = GameUtils.RequireManager<FlowControllerBase>();
				m_flowController = flowControllerBase.gameObject.RequireComponent<ClientHordeFlowController>();
				m_health = m_flowController.Health;
			}
		}

		private void Update()
		{
			if (!(m_flowController != null))
			{
				return;
			}
			float num = m_flowController.Health;
			float num2 = Mathf.Abs(num - m_health);
			if (num2 > 0f && base.isActiveAndEnabled)
			{
				GameObject obj = GameUtils.InstantiateUIControllerOnScalingHUDCanvas(m_removePointsFloatingTextPrefab);
				RectTransform rectTransform = (RectTransform)base.transform;
				RectTransformExtension rectTransformExtension = obj.RequireComponent<RectTransformExtension>();
				Vector2 anchorOffset = m_floatingTextOffset.MultipliedBy(rectTransform.anchorMin + rectTransform.anchorMax);
				rectTransformExtension.AnchorOffset = anchorOffset;
				obj.RequireComponent<DisplayIntUIController>().Value = Mathf.RoundToInt(Mathf.Abs(num2));
				m_animator.SetTrigger(m_healthAlertId);
				if (m_pulseStart != 0f && num > 0f && num <= m_pulseStart)
				{
					m_animator.SetTrigger(m_healthPulseId);
				}
				else if (num > 0f)
				{
					m_animator.SetTrigger(m_healthAlertId);
				}
			}
			m_healthBar.Value = num / (float)m_flowController.MaxHealth;
			m_health = num;
		}
	}
}
