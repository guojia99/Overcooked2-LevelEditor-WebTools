using System;
using Steamworks;

namespace Team17.Online
{
	public class SteamOnlineMultiplayerGameInviteCoordinator : IOnlineMultiplayerGameInviteCoordinator
	{
		private static Callback<GameLobbyJoinRequested_t> s_LobbyJoinRequestedCallback;

		private bool m_isInitialized;

		private OnlineMultiplayerSessionInvite m_acceptedInvite;

		public void Initialize()
		{
			if (!m_isInitialized)
			{
				s_LobbyJoinRequestedCallback = Callback<GameLobbyJoinRequested_t>.Create(OnLobbyJoinRequested);
				m_isInitialized = true;
				CheckForBootInvite();
			}
		}

		public OnlineMultiplayerSessionInvite InviteAccepted()
		{
			OnlineMultiplayerSessionInvite acceptedInvite = m_acceptedInvite;
			m_acceptedInvite = null;
			return acceptedInvite;
		}

		public bool HasPendingAcceptedInvite()
		{
			return m_acceptedInvite != null;
		}

		public OnlineMultiplayerSessionPlayTogetherHosting PlayTogetherHosting()
		{
			return null;
		}

		public void Update()
		{
			if (!m_isInitialized)
			{
			}
		}

		private void OnLobbyJoinRequested(GameLobbyJoinRequested_t pCallback)
		{
			m_acceptedInvite = new OnlineMultiplayerSessionInvite
			{
				m_steamLobbyId = pCallback.m_steamIDLobby
			};
		}

		private void CheckForBootInvite()
		{
			try
			{
				string[] commandLineArgs = Environment.GetCommandLineArgs();
				if (commandLineArgs == null || commandLineArgs.Length < 2)
				{
					return;
				}
				for (int i = 0; i < commandLineArgs.Length; i++)
				{
					if (!(commandLineArgs[i] == "+connect_lobby"))
					{
						continue;
					}
					if (i + 1 >= commandLineArgs.Length || string.IsNullOrEmpty(commandLineArgs[i + 1]))
					{
						break;
					}
					ulong result = 0uL;
					if (ulong.TryParse(commandLineArgs[i + 1], out result))
					{
						CSteamID steamLobbyId = new CSteamID(result);
						if (steamLobbyId.IsValid() && steamLobbyId.IsLobby())
						{
							m_acceptedInvite = new OnlineMultiplayerSessionInvite
							{
								m_steamLobbyId = steamLobbyId
							};
						}
					}
					break;
				}
			}
			catch (Exception)
			{
			}
		}
	}
}
