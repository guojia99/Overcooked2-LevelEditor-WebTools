using System.Collections.Generic;
using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

[ExecutionDependency(typeof(PlayerInputLookup))]
public class CompetitiveKitchenLoaderManager : KitchenLoaderManager
{
	[SerializeField]
	[AssignResource("Red", Editorbility.Editable)]
	private ChefColourData m_red;

	[SerializeField]
	[AssignResource("Blue", Editorbility.Editable)]
	private ChefColourData m_blue;

	[SerializeField]
	private PlayerInputLookup.Player[] m_redPlayersInScene = new PlayerInputLookup.Player[2]
	{
		PlayerInputLookup.Player.One,
		PlayerInputLookup.Player.Three
	};

	[SerializeField]
	private PlayerInputLookup.Player[] m_bluePlayersInScene = new PlayerInputLookup.Player[2]
	{
		PlayerInputLookup.Player.Two,
		PlayerInputLookup.Player.Four
	};

	private FastList<User> m_Team1Users = new FastList<User>(2);

	private FastList<User> m_Team2Users = new FastList<User>(2);

	private void AssignChefsForTeam(TeamID team, FastList<User> teamUsers)
	{
		if (teamUsers.Count == 0)
		{
			return;
		}
		int num = 0;
		for (int i = 0; i < PlayerIDProvider.s_AllProviders.Count; i++)
		{
			PlayerIDProvider playerIDProvider = PlayerIDProvider.s_AllProviders._items[i];
			if (playerIDProvider != null && playerIDProvider.GetTeam() == team)
			{
				if (num >= teamUsers.Count)
				{
					teamUsers._items[0].Entity2ID = EntitySerialisationRegistry.GetEntry(playerIDProvider.gameObject).m_Header.m_uEntityID;
					break;
				}
				teamUsers._items[num].EntityID = EntitySerialisationRegistry.GetEntry(playerIDProvider.gameObject).m_Header.m_uEntityID;
				num++;
			}
		}
	}

	private void AutoAssignUnassignedUsersATeam(FastList<User> users)
	{
		int count = users.Count;
		for (int i = 0; i < count; i++)
		{
			User user = users._items[i];
			if (user.Team != TeamID.None && user.Team != TeamID.Count)
			{
				continue;
			}
			if (m_Team1Users.Count == m_Team2Users.Count)
			{
				if (Random.Range(0, 1) == 0)
				{
					user.Team = TeamID.One;
					m_Team1Users.Add(user);
				}
				else
				{
					user.Team = TeamID.Two;
					m_Team2Users.Add(user);
				}
			}
			else if (m_Team1Users.Count > m_Team2Users.Count)
			{
				user.Team = TeamID.Two;
				m_Team2Users.Add(user);
			}
			else
			{
				user.Team = TeamID.One;
				m_Team1Users.Add(user);
			}
		}
	}

	public override void AssignChefEntities(FastList<User> users)
	{
		for (int i = 0; i < PlayerIDProvider.s_AllProviders.Count; i++)
		{
			PlayerIDProvider playerIDProvider = PlayerIDProvider.s_AllProviders._items[i];
			PlayerInputLookup.Player iD = playerIDProvider.GetID();
			if (m_redPlayersInScene.Contains(iD))
			{
				playerIDProvider.AssignToTeam(TeamID.One);
			}
			else
			{
				playerIDProvider.AssignToTeam(TeamID.Two);
			}
		}
		m_Team1Users.Clear();
		m_Team2Users.Clear();
		int count = users.Count;
		for (int j = 0; j < count; j++)
		{
			User user = users._items[j];
			if (user.Team == TeamID.One)
			{
				m_Team1Users.Add(user);
			}
			else if (user.Team == TeamID.Two)
			{
				m_Team2Users.Add(user);
			}
		}
		AutoAssignUnassignedUsersATeam(users);
		uint colour = 7u;
		uint colour2 = 7u;
		AvatarDirectoryData avatarDirectoryData = GameUtils.GetAvatarDirectoryData();
		for (uint num = 0u; num < avatarDirectoryData.Colours.Length; num++)
		{
			if (avatarDirectoryData.Colours[num] == m_red)
			{
				colour = num;
			}
			if (avatarDirectoryData.Colours[num] == m_blue)
			{
				colour2 = num;
			}
		}
		for (int k = 0; k < m_Team1Users.Count; k++)
		{
			m_Team1Users._items[k].Colour = colour;
		}
		for (int l = 0; l < m_Team2Users.Count; l++)
		{
			m_Team2Users._items[l].Colour = colour2;
		}
		AssignChefsForTeam(TeamID.One, m_Team1Users);
		AssignChefsForTeam(TeamID.Two, m_Team2Users);
		if ((m_Team1Users.Count == 0 || m_Team2Users.Count == 0) && ConnectionStatus.IsInSession() && ConnectionStatus.IsHost())
		{
			NetworkErrors.CachedErrorTitle = "Text.Versus.NotEnoughPlayers.Title";
			NetworkErrors.CachedErrorMessage = "Text.Versus.NotEnoughPlayers.Message";
			ServerMessenger.LoadLevel(GameUtils.GetGameSession().TypeSettings.WorldMapScene, GameState.VSLobby, true);
		}
	}
}
