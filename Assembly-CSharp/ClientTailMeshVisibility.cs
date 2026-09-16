using Team17.Online;
using Team17.Online.Multiplayer.Messaging;

public class ClientTailMeshVisibility : ClientMeshVisibilityBase<TailMeshVisibility.VisState>
{
	private void Awake()
	{
		Mailbox.Client.RegisterForMessageType(MessageType.GameState, OnGameStateChanged);
	}

	protected override void OnDestroy()
	{
		Mailbox.Client.UnregisterForMessageType(MessageType.GameState, OnGameStateChanged);
		base.OnDestroy();
	}

	private void OnGameStateChanged(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
	{
		GameStateMessage gameStateMessage = (GameStateMessage)message;
		if (gameStateMessage.m_State == GameState.StartEntities)
		{
			TailMeshVisibility.VisState initialVisState = base.gameObject.RequireComponent<TailMeshVisibility>().m_initialVisState;
			Setup(initialVisState);
		}
	}

	public void ForceSetup()
	{
		Setup(base.gameObject.RequireComponent<TailMeshVisibility>().m_initialVisState);
	}

	public void SetVisState(TailMeshVisibility.VisState _visState)
	{
		SetState(_visState);
	}
}
