using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

namespace GameModes.Horde
{
	public class MoneyUIController : UIControllerAndContainer
	{
		[SerializeField]
		private Animator m_animator;

		[Header("Floating Text")]
		[SerializeField]
		[AssignResource("AddPointsFloatingNumberUI", Editorbility.Editable)]
		private GameObject m_addMoneyFloatingTextPrefab;

		[SerializeField]
		[AssignResource("RemovePointsFloatingNumberUI", Editorbility.Editable)]
		private GameObject m_removeMoneyFloatingTextPrefab;

		[SerializeField]
		private Vector2 m_floatingTextOffset = new Vector2(0.5f, 0.5f);

		[SerializeField]
		private T17Text m_moneyText;

		[SerializeField]
		private string m_coinSpinTriggerString = string.Empty;

		private int m_coinSpinTriggerId;

		private ClientHordeFlowController m_flowController;

		private int m_money;

		protected void Awake()
		{
			m_moneyText.SetNonLocalizedText("0");
			Mailbox.Client.RegisterForMessageType(MessageType.GameState, OnGameStateChanged);
		}

		protected void OnDestroy()
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
			}
		}

		private void Update()
		{
			if (m_flowController != null)
			{
				int money = m_flowController.Money;
				int num = money - m_money;
				if (num != 0 && base.isActiveAndEnabled)
				{
					m_animator.SetTrigger(m_coinSpinTriggerId);
					GameObject obj = GameUtils.InstantiateUIControllerOnScalingHUDCanvas((num <= 0) ? m_removeMoneyFloatingTextPrefab : m_addMoneyFloatingTextPrefab);
					RectTransform rectTransform = (RectTransform)base.transform;
					RectTransformExtension rectTransformExtension = obj.RequireComponent<RectTransformExtension>();
					Vector2 anchorOffset = m_floatingTextOffset.MultipliedBy(rectTransform.anchorMin + rectTransform.anchorMax);
					rectTransformExtension.AnchorOffset = anchorOffset;
					obj.RequireComponent<DisplayIntUIController>().Value = Mathf.Abs(num);
					m_moneyText.SetNonLocalizedText(money.ToString());
				}
				m_money = money;
			}
		}
	}
}
