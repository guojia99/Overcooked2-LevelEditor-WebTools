using UnityEngine;

public class ClientTeamMonitor
{
	private TeamMonitor m_monitor;

	private ClientOrderControllerBase m_ordersController;

	private PlayerInputLookup.Player[] m_players;

	private TeamMonitor.TeamScoreStats m_score = new TeamMonitor.TeamScoreStats();

	public TeamMonitor.TeamScoreStats Score
	{
		get
		{
			return m_score;
		}
	}

	public ClientOrderControllerBase OrdersController
	{
		get
		{
			return m_ordersController;
		}
	}

	public PlayerInputLookup.Player[] Members
	{
		get
		{
			return m_players;
		}
	}

	public virtual void Update()
	{
		m_ordersController.Update();
	}

	public virtual void Initialise(TeamMonitor _monitor, TeamID _teamID, ClientOrderControllerBuilder _controllerBuilder)
	{
		m_monitor = _monitor;
		m_ordersController = _controllerBuilder(m_monitor.m_recipeBarUIController);
		GameObject[] array = GameObject.FindGameObjectsWithTag("Player");
		array = array.FindAll((GameObject x) => x.RequireComponent<PlayerIDProvider>().GetTeam() == _teamID);
		m_players = array.ConvertAll((GameObject x) => x.RequireComponent<PlayerIDProvider>().GetID());
	}
}
