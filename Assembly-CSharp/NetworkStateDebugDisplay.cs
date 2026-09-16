using System.Collections.Generic;
using Team17.Online;
using Team17.Online.Multiplayer.Connection;
using UnityEngine;

internal class NetworkStateDebugDisplay : DebugDisplay
{
	private MultiplayerController m_MultiplayerController;

	private IOnlineMultiplayerConnectionModeCoordinator m_ConnectionModeCoordinator;

	public override void OnSetUp()
	{
		m_MultiplayerController = GameUtils.RequireManager<MultiplayerController>();
		IOnlinePlatformManager onlinePlatformManager = GameUtils.RequireManagerInterface<IOnlinePlatformManager>();
		m_ConnectionModeCoordinator = onlinePlatformManager.OnlineMultiplayerConnectionModeCoordinator();
	}

	public override void OnUpdate()
	{
	}

	public override void OnDraw(ref Rect rect, GUIStyle style)
	{
		string text = string.Empty;
		string text2 = string.Empty;
		if (ConnectionModeSwitcher.GetRequestedConnectionState() == NetConnectionState.Server)
		{
			ServerOptions serverOptions = (ServerOptions)ConnectionModeSwitcher.GetAgentData();
			text = ", " + serverOptions.visibility;
			text2 = ", " + serverOptions.gameMode;
		}
		else if (ConnectionModeSwitcher.GetRequestedConnectionState() == NetConnectionState.Matchmake)
		{
			MatchmakeData matchmakeData = (MatchmakeData)ConnectionModeSwitcher.GetAgentData();
			if (ConnectionStatus.IsHost())
			{
				text = ", " + OnlineMultiplayerSessionVisibility.eMatchmaking;
			}
			text2 = ",  " + matchmakeData.gameMode;
		}
		DrawText(ref rect, style, ConnectionModeSwitcher.GetRequestedConnectionState().ToString() + text + text2 + ", " + ConnectionModeSwitcher.GetStatus().GetProgress().ToString() + " " + ConnectionModeSwitcher.GetStatus().GetResult());
		DrawText(ref rect, style, string.Concat(ClientGameSetup.Mode, ", time: ", ClientTime.Time().ToString("0000.000")));
		if (ConnectionStatus.IsHost())
		{
			FastList<ConnectionStats> serverConnectionStats = m_MultiplayerController.GetServerConnectionStats(true);
			FastList<ConnectionStats> serverConnectionStats2 = m_MultiplayerController.GetServerConnectionStats(false);
			if (serverConnectionStats.Count > 0)
			{
				string empty = string.Empty;
				for (int i = 0; i < serverConnectionStats.Count; i++)
				{
					DrawText(ref rect, style, "RLag: " + (serverConnectionStats._items[i].m_fLatency * 1000f).ToString("000") + " MaxWait: " + serverConnectionStats._items[i].m_fMaxTimeBetweenReceives.ToString("00.00") + " Sequence: I" + serverConnectionStats._items[i].m_fIncomingSequenceNumber + " / O" + serverConnectionStats._items[i].m_fOutgoingSequenceNumber);
				}
				DrawText(ref rect, style, empty);
				empty = string.Empty;
				for (int j = 0; j < serverConnectionStats.Count; j++)
				{
					DrawText(ref rect, style, "ULag: " + (serverConnectionStats2._items[j].m_fLatency * 1000f).ToString("000") + " MaxWait: " + serverConnectionStats2._items[j].m_fMaxTimeBetweenReceives.ToString("00.00") + " Sequence: I" + serverConnectionStats2._items[j].m_fIncomingSequenceNumber + " / O" + serverConnectionStats2._items[j].m_fOutgoingSequenceNumber);
				}
			}
		}
		else if (ConnectionStatus.IsInSession())
		{
			ConnectionStats clientConnectionStats = m_MultiplayerController.GetClientConnectionStats(true);
			ConnectionStats clientConnectionStats2 = m_MultiplayerController.GetClientConnectionStats(false);
			DrawText(ref rect, style, "RLag: " + (clientConnectionStats.m_fLatency * 1000f).ToString("000") + " MaxWait: " + clientConnectionStats.m_fMaxTimeBetweenReceives.ToString("00.00") + " Sequence: I" + clientConnectionStats.m_fIncomingSequenceNumber + " / O" + clientConnectionStats.m_fOutgoingSequenceNumber);
			DrawText(ref rect, style, "ULag: " + (clientConnectionStats2.m_fLatency * 1000f).ToString("000") + " MaxWait: " + clientConnectionStats2.m_fMaxTimeBetweenReceives.ToString("00.00") + " Sequence: I" + clientConnectionStats2.m_fIncomingSequenceNumber + " / O" + clientConnectionStats2.m_fOutgoingSequenceNumber);
		}
		if (m_ConnectionModeCoordinator != null)
		{
			DrawText(ref rect, style, m_ConnectionModeCoordinator.DebugStatus());
		}
	}
}
