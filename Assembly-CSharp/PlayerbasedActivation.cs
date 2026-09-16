using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class PlayerbasedActivation : MonoBehaviour
{
	public enum PlayerCount
	{
		One = 0,
		Two = 1,
		Three = 2,
		Four = 3
	}

	[SerializeField]
	[Mask(typeof(PlayerCount))]
	public int m_activePlayerCount = -1;

	private void Awake()
	{
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
			UpdateActiveState();
		}
	}

	private void UpdateActiveState()
	{
		bool flag = IsActiveForPlayerCount(ClientUserSystem.m_Users.Count);
		if (base.gameObject.activeSelf != flag)
		{
			base.gameObject.SetActive(flag);
		}
	}

	private bool IsActiveForPlayerCount(int _count)
	{
		int num = _count - 1;
		if (num >= 0)
		{
			return (m_activePlayerCount & (1 << num)) > 0;
		}
		return false;
	}
}
