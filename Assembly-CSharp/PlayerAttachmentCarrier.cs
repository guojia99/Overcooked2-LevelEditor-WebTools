using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class PlayerAttachmentCarrier : MonoBehaviour, IParentable
{
	private Transform[] m_attachPoints = new Transform[2];

	private void Awake()
	{
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
			m_attachPoints[0] = base.transform.FindChildRecursive("Attachment").transform;
			m_attachPoints[1] = base.transform.FindChildRecursive("Attachment_Backpack").transform;
		}
	}

	public Transform GetAttachPoint(GameObject gameObject)
	{
		if (gameObject != null)
		{
			IHandleAttachTarget handleAttachTarget = gameObject.RequestInterface<IHandleAttachTarget>();
			if (handleAttachTarget as MonoBehaviour != null)
			{
				return m_attachPoints[(int)handleAttachTarget.PlayerAttachTarget];
			}
		}
		return m_attachPoints[0];
	}

	public Transform GetAttachPoint(PlayerAttachTarget playerAttachTarget)
	{
		return m_attachPoints[(int)playerAttachTarget];
	}

	public bool HasClientSidePrediction()
	{
		return false;
	}
}
