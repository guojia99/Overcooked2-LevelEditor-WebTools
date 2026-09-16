using Team17.Online;
using Team17.Online.Multiplayer.Messaging;

public class ClientHeldItemsMeshVisibility : ClientMeshVisibilityBase<HeldItemsMeshVisibility.VisState>
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
			Setup(HeldItemsMeshVisibility.VisState.Idle);
		}
	}

	public void ForceSetup()
	{
		Setup(HeldItemsMeshVisibility.VisState.Idle);
	}

	public void SetVisState(HeldItemsMeshVisibility.VisState _visState)
	{
		SetState(_visState);
	}
}
