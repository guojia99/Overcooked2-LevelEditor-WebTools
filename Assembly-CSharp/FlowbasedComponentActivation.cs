using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

[ExecutionDependency(typeof(IFlowController))]
public class FlowbasedComponentActivation : MonoBehaviour
{
	[SerializeField]
	private Behaviour m_targetComponent;

	[SerializeField]
	private bool m_activeInRound = true;

	[SerializeField]
	private bool m_activeOutOfRound;

	private IFlowController m_iFlowController;

	private void OnValidate()
	{
		if (m_targetComponent != null)
		{
			m_targetComponent.enabled = m_activeOutOfRound;
		}
	}

	private void Awake()
	{
		m_targetComponent.enabled = m_activeOutOfRound;
		Mailbox.Client.RegisterForMessageType(MessageType.GameState, OnGameStateChanged);
	}

	private void OnGameStateChanged(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
	{
		GameStateMessage gameStateMessage = (GameStateMessage)message;
		if (gameStateMessage.m_State == GameState.StartEntities)
		{
			Initialise();
		}
	}

	private void Initialise()
	{
		m_iFlowController = GameUtils.GetFlowController();
		if (m_iFlowController != null)
		{
			m_iFlowController.RoundActivatedCallback += OnBegun;
			m_iFlowController.RoundDeactivatedCallback += OnEnded;
		}
	}

	private void OnDestroy()
	{
		Mailbox.Client.UnregisterForMessageType(MessageType.GameState, OnGameStateChanged);
		if (m_iFlowController != null)
		{
			m_iFlowController.RoundActivatedCallback -= OnBegun;
			m_iFlowController.RoundDeactivatedCallback -= OnEnded;
		}
	}

	private void OnBegun()
	{
		m_targetComponent.enabled = m_activeInRound;
	}

	private void OnEnded()
	{
		m_targetComponent.enabled = m_activeOutOfRound;
	}
}
