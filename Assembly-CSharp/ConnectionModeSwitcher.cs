using System.Collections.Generic;
using Team17.Online.Multiplayer;

public class ConnectionModeSwitcher
{
	private static OfflineAgent m_OfflineAgent = new OfflineAgent();

	private static ServerAgent m_ServerAgent = new ServerAgent();

	private static AcceptInviteAgent m_AcceptInviteAgent = new AcceptInviteAgent();

	private static JoinEnumeratedRoomAgent m_JoinEnumeratedRoomAgent = new JoinEnumeratedRoomAgent();

	private static MatchmakingAgent m_MatchmakingAgent = new MatchmakingAgent();

	private static Dictionary<NetConnectionState, ConnectionModeAgent> m_AgentMap = new Dictionary<NetConnectionState, ConnectionModeAgent>(5);

	private static ConnectionModeAgent m_CurrentAgent = m_OfflineAgent;

	private static NetConnectionState m_CurrentAgentState = NetConnectionState.Offline;

	private static Server m_LocalServer = null;

	private static Client m_LocalClient = null;

	public static void Initialise(Server server, Client client)
	{
		m_LocalServer = server;
		m_LocalClient = client;
		m_AgentMap.Add(NetConnectionState.Offline, m_OfflineAgent);
		m_AgentMap.Add(NetConnectionState.Server, m_ServerAgent);
		m_AgentMap.Add(NetConnectionState.AcceptInvite, m_AcceptInviteAgent);
		m_AgentMap.Add(NetConnectionState.JoinEnumeratedRoom, m_JoinEnumeratedRoomAgent);
		m_AgentMap.Add(NetConnectionState.Matchmake, m_MatchmakingAgent);
	}

	public static bool RequestConnectionState(NetConnectionState state, object data = null, GenericVoid<IConnectionModeSwitchStatus> callback = null)
	{
		ConnectionModeAgent connectionModeAgent = m_AgentMap[state];
		if (connectionModeAgent != m_CurrentAgent)
		{
			MultiplayerController multiplayerController = GameUtils.RequireManager<MultiplayerController>();
			multiplayerController.StopSynchronisation();
			if (m_CurrentAgent != null)
			{
				m_CurrentAgent.Stop();
			}
			m_CurrentAgent = connectionModeAgent;
			m_CurrentAgentState = state;
		}
		return m_CurrentAgent.Start(m_LocalServer, m_LocalClient, data, callback);
	}

	public static void InvalidateCallback(GenericVoid<IConnectionModeSwitchStatus> callback)
	{
		m_CurrentAgent.InvalidateCallback(callback);
	}

	public static NetConnectionState GetRequestedConnectionState()
	{
		return m_CurrentAgentState;
	}

	public static IConnectionModeSwitchStatus GetStatus()
	{
		return m_CurrentAgent.GetStatus();
	}

	public static object GetAgentData()
	{
		return m_CurrentAgent.GetAgentData();
	}

	public static void Update()
	{
		if (m_CurrentAgent != null)
		{
			m_CurrentAgent.Update();
		}
	}
}
