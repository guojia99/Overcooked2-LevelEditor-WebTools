using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientTime
{
	private static float m_fDelta;

	private static float m_fLocalRunningTime;

	private static float m_fLocalTimeLastFrame;

	private static MultiplayerController m_MultiplayerController;

	private static float m_fLastReceivedTime;

	private static float m_fCurrentOffset;

	private static float m_fOldOffset;

	public void Initialise()
	{
		m_MultiplayerController = GameUtils.RequireManager<MultiplayerController>();
		Mailbox.Client.RegisterForMessageType(MessageType.TimeSync, OnTimeSyncReceived);
	}

	public void Shutdown()
	{
		Mailbox.Client.UnregisterForMessageType(MessageType.TimeSync, OnTimeSyncReceived);
	}

	public static float Time()
	{
		return m_fLocalRunningTime + Mathf.Lerp(m_fOldOffset, m_fCurrentOffset, (UnityEngine.Time.realtimeSinceStartup - m_fLastReceivedTime) / 3f);
	}

	public static float DeltaTime()
	{
		return m_fDelta;
	}

	public static void Update()
	{
		float realtimeSinceStartup = UnityEngine.Time.realtimeSinceStartup;
		m_fDelta = realtimeSinceStartup - m_fLocalTimeLastFrame;
		m_fLocalRunningTime += m_fDelta;
		m_fLocalTimeLastFrame = realtimeSinceStartup;
	}

	private void OnTimeSyncReceived(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
	{
		TimeSyncMessage timeSyncMessage = (TimeSyncMessage)message;
		float fLatency = m_MultiplayerController.GetClientConnectionStats(false).m_fLatency;
		m_fOldOffset = m_fCurrentOffset;
		m_fCurrentOffset = timeSyncMessage.fTime - m_fLocalRunningTime + fLatency;
		m_fLastReceivedTime = UnityEngine.Time.realtimeSinceStartup;
	}
}
