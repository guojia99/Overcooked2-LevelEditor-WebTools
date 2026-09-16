using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

[ExecutionDependency(typeof(IFlowController))]
public class SetShaderParameterAfterTime : MonoBehaviour
{
	[SerializeField]
	private MaterialScroll[] m_materialScrolls;

	private IFlowController flowController;

	private void Awake()
	{
		Mailbox.Client.RegisterForMessageType(MessageType.GameState, OnGameStateChanged);
	}

	private void Start()
	{
		MaterialScroll[] materialScrolls = m_materialScrolls;
		foreach (MaterialScroll materialScroll in materialScrolls)
		{
			materialScroll.SetMaterialScrollToZero();
		}
	}

	private void OnDestroy()
	{
		if (flowController != null)
		{
			flowController.RoundActivatedCallback -= OnFlowStart;
		}
		Mailbox.Client.UnregisterForMessageType(MessageType.GameState, OnGameStateChanged);
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
		flowController = GameUtils.GetFlowController();
		if (flowController != null)
		{
			flowController.RoundActivatedCallback += OnFlowStart;
		}
	}

	private void OnFlowStart()
	{
		MaterialScroll[] materialScrolls = m_materialScrolls;
		foreach (MaterialScroll materialScroll in materialScrolls)
		{
			materialScroll.SetMaterialScrollToValue();
		}
	}
}
